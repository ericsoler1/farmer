[<AutoOpen>]
module Farmer.Builders.VirtualHub

open Farmer
open Farmer.VirtualHub
open Farmer.Arm.VirtualWAN
open Farmer.Arm.VirtualHub


type VirtualHubConfig =
    { Name : ResourceName
      Sku : Sku
      AddressPrefix : IPAddressCidr option
      Vwan : LinkedResource option }
    interface IBuilder with
        member this.ResourceId = virtualWans.resourceId this.Name
        member this.BuildResources location = [
            { Name = this.Name
              Location = location
              Dependencies = [
                  match this.Vwan with
                  | Some (Managed resId) -> resId // Only generate dependency if this is managed by Farmer (same template)
                  | _ -> ()     
              ] |> Set.ofList
              AddressPrefix = this.AddressPrefix
              AllowBranchToBranchTraffic = None
              AzureFirewall = None
              ExpressRouteGateway = None
              P2SVpnGateway = None
              RouteTable = []
              RoutingState = None
              SecurityProvider = None
              SecurityPartnerProvider = None
              VirtualHubRouteTableV2s = []
              VirtualHubSku = this.Sku
              VirtualRouterAsn = None
              VirtualRouterIps = []
              VpnGateway = None
              VWAN =
                  match this.Vwan with
                  | Some (Managed resId) -> Some resId
                  | Some (Unmanaged resId) -> Some resId
                  | _ -> None }
        ]

type VirtualHubBuilder() =
    member _.Yield _ =
        { Name = ResourceName.Empty
          AddressPrefix = None
          Vwan = None
          Sku = Standard }
    [<CustomOperation "name">]
    /// Sets the name of the virtual hub.
    member _.Name(state:VirtualHubConfig, name) = { state with Name = name }
    member this.Name(state:VirtualHubConfig, name) = this.Name(state, ResourceName name)
    [<CustomOperation "address_prefix">]
    /// Sets the address prefix of the virtual hub.
    member _.AddressPrefix(state:VirtualHubConfig, addressPrefix) = { state with AddressPrefix = Some addressPrefix }
    [<CustomOperation "link_to_vwan">]
    /// Links the VirtualHub to a Farmer-managed VirtualWAN instance
    member _.LinkToVwan(state:VirtualHubConfig, vwanName:ResourceName) =
        { state with Vwan = Some (LinkedResource.Managed (virtualWans.resourceId vwanName))}
    member _.LinkToVwan(state:VirtualHubConfig, vwanName) =
        { state with Vwan = Some (LinkedResource.Managed (virtualWans.resourceId (ResourceName vwanName)))}
    [<CustomOperation "link_to_unmanaged_vwan">]
    /// Links the VirtualHub to an existing VirtualWAN instance
    member _.LinkToExternalVwan(state:VirtualHubConfig, vwanName:ResourceName) =
        { state with Vwan = Some (LinkedResource.Unmanaged (virtualWans.resourceId vwanName))}
    member _.LinkToExternalVwan(state:VirtualHubConfig, vwanName) =
        { state with Vwan = Some (LinkedResource.Unmanaged (virtualWans.resourceId (ResourceName vwanName)))}
     /// The SKU of the virtual hub.
    [<CustomOperation "sku">]
    member _.Sku(state:VirtualHubConfig, sku) = { state with Sku = sku }
    
    
let vhub = VirtualHubBuilder()
    