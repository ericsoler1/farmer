[<AutoOpen>]
module Farmer.Builders.VirtualHub

open Farmer
open Farmer.VirtualHub
open Farmer.Arm.VirtualWan
open Farmer.Arm.VirtualHub
open Farmer.VirtualHub.HubRouteTable

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
              Vwan =
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
    
type HubRouteTableConfig =
    { Name : ResourceName
      Vhub : LinkedResource
      Routes : HubRoute list }
    interface IBuilder with
        member this.ResourceId =
            let vhubResourceId = 
                match this.Vhub with
                | Unmanaged resId
                | Managed resId -> resId
            hubRouteTables.resourceId (vhubResourceId.Name/this.Name)
        member this.BuildResources location = [
            { Name = this.Name
              VirtualHub =
                  match this.Vhub with
                  | Unmanaged resId
                  | Managed resId -> resId
              Dependencies = [
                match this.Vhub with
                | Managed resId -> resId // Only generate dependency if this is managed by Farmer (same template)
                | _ -> ()
                
                let routeDependencies =
                    this.Routes
                    |> List.map
                           (fun r -> [
                                match r.NextHop with
                                | NextHop.ResourceId (Managed resId) -> resId
                                | _ -> ()
                                match r.Destination with
                                | _ -> ()
                            ])
                    |> List.concat
                for routeDep in routeDependencies do
                    routeDep
              ] |> Set.ofList
              Routes = this.Routes
              Labels = [] }
        ]
        
type HubRouteTableBuilder() =
    member _.Yield _ =
        { Name = ResourceName.Empty
          Vhub = LinkedResource.Unmanaged (virtualHubs.resourceId ResourceName.Empty)
          Routes = List.Empty }
    [<CustomOperation "name">]
    /// Sets the name of the virtual hub.
    member _.Name(state:HubRouteTableConfig, name) = { state with Name = name }
    member _.Name(state:HubRouteTableConfig, name) = { state with Name = (ResourceName name) }
    [<CustomOperation "link_to_vhub">]
    /// Links the HubRouteTable to a Farmer-managed VirtualHub instance
    member _.LinkToVhub(state:HubRouteTableConfig, vhubName:ResourceName) =
        { state with Vhub = LinkedResource.Managed (virtualHubs.resourceId vhubName)}
    member _.LinkToVhub(state:HubRouteTableConfig, vhubName) =
        { state with Vhub = LinkedResource.Managed (virtualHubs.resourceId (ResourceName vhubName))}
    [<CustomOperation "link_to_unmanaged_vhub">]
    /// Links the HubRouteTable to an existing VirtualHub instance
    member _.LinkToExternalVhub(state:HubRouteTableConfig, vhubName:ResourceName) =
        { state with Vhub = LinkedResource.Unmanaged (virtualHubs.resourceId vhubName)}
    member _.LinkToExternalVhub(state:HubRouteTableConfig, vhubName) =
        { state with Vhub = LinkedResource.Unmanaged (virtualHubs.resourceId (ResourceName vhubName))}
    [<CustomOperation "add_routes">]
    /// Adds the routes to the HubRouteTable
    member _.AddRoutes(state:HubRouteTableConfig, routes) =
        // TODO: does validation about conflicting routes belong here
        { state with Routes = state.Routes @ routes}
    member _.Run (state:HubRouteTableConfig) =
        match state.Vhub with
        | Managed resourceId
        | Unmanaged resourceId when resourceId.Name <> ResourceName.Empty ->
            state
        | _ -> failwith $"HubRouteTable '{state.Name}' must specify link_to_vhub or link_to_unmanaged_vhub"


let vhub = VirtualHubBuilder()
let hubRouteTable = HubRouteTableBuilder()
