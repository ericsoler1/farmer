module VirtualHub

open Expecto
open Farmer
open Farmer.Arm.VirtualHub
open Farmer.Builders
open Farmer.VirtualHub
open Microsoft.Azure.Management.Network
open Microsoft.Azure.Management.Network.Models
open Microsoft.Rest
open System
open Microsoft.Rest.Serialization

let getResource<'T when 'T :> IArmResource> (data:IArmResource list) = data |> List.choose(function :? 'T as x -> Some x | _ -> None)

let getVirtualHubResource = getResource<Farmer.Arm.VirtualHub.VirtualHub>
/// Client instance needed to get the serializer settings.
let dummyClient = new NetworkManagementClient (Uri "http://management.azure.com", TokenCredentials "NotNullOrWhiteSpace")
let getResourceAtIndex o = o |> getResourceAtIndex dummyClient.SerializationSettings

let getResources (v:IBuilder) = v.BuildResources Location.WestUS

let getResourceDependsOnByName (template:Deployment) (resourceName:ResourceName) =
    let json = template.Template |> Writer.toJson
    let jobj = Newtonsoft.Json.Linq.JObject.Parse(json)
    let dependsOn = jobj.SelectToken($"resources[?(@.name=='{resourceName.Value}')].dependsOn")
    let jarray = dependsOn :?> Newtonsoft.Json.Linq.JArray
    [for jvalue in jarray do jvalue.ToString()]
   
let asAzureResource (virtualHub:VirtualHubConfig) =
    arm { add_resource virtualHub }
    |> findAzureResources<VirtualHub> dummyClient.SerializationSettings
    |> List.head
    |> fun r ->
        r
let tests = testList "Virtual Hub Tests" [
    test "VirtualHub is correctly created" { 
        let vhub =
            vhub {
                name "my-vhub"                
            }
            |> asAzureResource
        Expect.equal vhub.Name "my-vhub" ""
        Expect.equal vhub.Sku Sku.Standard.ArmValue ""
    }
    test "VirtualHub with address prefix" {
        let expectedAddressPrefix = (IPAddressCidr.parse "10.0.0.0/24")
        let vhub =
            vhub {
                name "my-vhub"
                address_prefix expectedAddressPrefix
            }
            |> asAzureResource
        Expect.equal vhub.AddressPrefix (IPAddressCidr.format expectedAddressPrefix) ""
    }
    test "VirtualHub does not create resources for unmanaged linked resources" {
        let resources =
            vhub {
                name "my-vhub"
                link_to_unmanaged_vwan "my-vwan"
            }
            |> getResources
        Expect.hasLength resources 1 ""
    }
    test "VirtualHub does not create resources for managed linked resources" {
        let resources =
            vhub {
                name "my-vhub"
                link_to_vwan "my-vwan"
            }
            |> getResources
        Expect.hasLength resources 1 ""
    }
    test "VirtualHub does not create dependencies for unmanaged linked resources" {
        let resource =
            vhub {
                name "my-vhub"
                link_to_unmanaged_vwan "my-vwan"
            }
            |> getResources |> getVirtualHubResource |> List.head
        Expect.isEmpty resource.Dependencies ""
    }
    test "VirtualHub creates dependencies for managed linked resources" {
        let resource =
            vhub {
                name "my-vhub"
                link_to_vwan "my-vwan"
            }
            |> getResources |> getVirtualHubResource |> List.head
        Expect.containsAll resource.Dependencies [
            ResourceId.create(Farmer.Arm.VirtualWAN.virtualWans, ResourceName "my-vwan");]
            ""
    }
    test "VirtualHub creates empty dependsOn in arm template json for unmanaged linked resources" {
        let template =
            arm {
                add_resources [
                    vhub {
                        name "my-vhub"
                        link_to_unmanaged_vwan "my-vwan"
                    }
                ]
            }
        let dependsOn = getResourceDependsOnByName template (ResourceName "my-vhub")    
        Expect.hasLength dependsOn 0 ""
    }
    test "VirtualHub creates dependsOn in arm template json for managed linked resources" {
        let template =
            arm {
                add_resources [
                    vhub {
                        name "my-vhub"
                        link_to_vwan "my-vwan"
                    }
                ]
            }
        let dependsOn = getResourceDependsOnByName template (ResourceName "my-vhub")
        Expect.hasLength dependsOn 1 ""
        let expectedVwanDependency = "[resourceId('Microsoft.Network/virtualWans', 'my-vwan')]"
        Expect.equal dependsOn.Head expectedVwanDependency "" 
    }
]