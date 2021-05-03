[<AutoOpen>]
module Farmer.Builders.ServiceBus

open Farmer
open Farmer.ServiceBus
open Farmer.Arm.ServiceBus
open Namespaces
open Topics
open System

type ServiceBusQueueConfig =
    { Name : ResourceName
      LockDuration : TimeSpan option
      DuplicateDetection : TimeSpan option
      DefaultMessageTimeToLive : TimeSpan option
      Session : bool option
      DeadLetteringOnMessageExpiration : bool option
      MaxDeliveryCount : int option
      EnablePartitioning : bool option }

type ServiceBusQueueBuilder() =
    member _.Yield _ =
        { Name = ResourceName.Empty
          LockDuration = None
          DuplicateDetection = None
          Session = None
          DeadLetteringOnMessageExpiration = None
          DefaultMessageTimeToLive = None
          MaxDeliveryCount = None
          EnablePartitioning = None }

    /// The name of the queue.
    [<CustomOperation "name">] member _.Name(state:ServiceBusQueueConfig, name) = { state with Name = ResourceName name }
    /// The length of time that a lock can be held on a message.
    [<CustomOperation "lock_duration_minutes">] member _.LockDurationMinutes(state:ServiceBusQueueConfig, duration) = { state with LockDuration = Some (TimeSpan.FromMinutes (float duration)) }
    /// The maximum number of times a message can be delivered before dead lettering.
    [<CustomOperation "duplicate_detection_minutes">] member _.DuplicateDetection(state:ServiceBusQueueConfig, maxTimeWindow) = { state with DuplicateDetection = Some (TimeSpan.FromMinutes (float maxTimeWindow)) }
    /// The default time-to-live for messages in a timespan string (e.g. '00:05:00'). If not specified, the maximum TTL will be set for the SKU.
    [<CustomOperation "message_ttl">]
    member _.MessageTtl(state:ServiceBusQueueConfig, ttl) = { state with DefaultMessageTimeToLive = Some (TimeSpan.Parse ttl) }
    /// The default time-to-live for messages in days. If not specified, the maximum TTL will be set for the SKU.
    member _.MessageTtl(state:ServiceBusQueueConfig, ttl:int<Days>) = { state with DefaultMessageTimeToLive = ttl / 1<Days> |> float |> TimeSpan.FromDays |> Some }
    /// Enables session support.
    [<CustomOperation "max_delivery_count">] member _.MaxDeliveryCount(state:ServiceBusQueueConfig, count) = { state with MaxDeliveryCount = Some count }
    /// Whether to enable duplicate detection, and if so, how long to check for.ServiceBusQueueConfig
    [<CustomOperation "enable_session">] member _.Session(state:ServiceBusQueueConfig) = { state with Session = Some true }
    /// Enables dead lettering of messages that expire.
    [<CustomOperation "enable_dead_letter_on_message_expiration">] member _.DeadLetteringOnMessageExpiration(state:ServiceBusQueueConfig) = { state with DeadLetteringOnMessageExpiration = Some true }
    /// Enables partition support on the queue.
    [<CustomOperation "enable_partition">] member _.EnablePartition(state:ServiceBusQueueConfig) = { state with EnablePartitioning = Some true }

type ServiceBusSubscriptionConfig =
    { Name : ResourceName
      LockDuration : TimeSpan option
      DuplicateDetection : TimeSpan option
      DefaultMessageTimeToLive : TimeSpan option
      MaxDeliveryCount : int option
      Session : bool option
      DeadLetteringOnMessageExpiration : bool option
      Rules : Rule list }
    interface IBuilder with
        member this.ResourceId = subscriptions.resourceId this.Name
        member this.BuildResources location = []

type ServiceBusSubscriptionBuilder() =
    member _.Yield _ =
        { Name = ResourceName.Empty
          LockDuration = None
          DuplicateDetection = None
          DefaultMessageTimeToLive = None
          MaxDeliveryCount = None
          Session = None
          DeadLetteringOnMessageExpiration = None
          Rules = List.empty }

    /// The name of the queue.
    [<CustomOperation "name">]
     member _.Name(state:ServiceBusSubscriptionConfig, name) = { state with Name = ResourceName name }
    /// The length of time that a lock can be held on a message.
    [<CustomOperation "lock_duration_minutes">]
     member _.LockDurationMinutes(state:ServiceBusSubscriptionConfig, duration) = { state with LockDuration = Some (TimeSpan.FromMinutes (float duration)) }
    /// Whether to enable duplicate detection, and if so, how long to check for.ServiceBusQueueConfig
    [<CustomOperation "duplicate_detection_minutes">]
     member _.DuplicateDetection(state:ServiceBusSubscriptionConfig, maxTimeWindow) = { state with DuplicateDetection = Some (TimeSpan.FromMinutes (float maxTimeWindow)) }
    /// Enables session support.
    [<CustomOperation "max_delivery_count">]
     member _.MaxDeliveryCount(state:ServiceBusSubscriptionConfig, count) = { state with MaxDeliveryCount = Some count }
    /// Whether to enable duplicate detection, and if so, how long to check for.ServiceBusQueueConfig
    [<CustomOperation "enable_session">]
     member _.Session(state:ServiceBusSubscriptionConfig) = { state with Session = Some true }
    /// Enables dead lettering of messages that expire.
    [<CustomOperation "enable_dead_letter_on_message_expiration">]
    member _.DeadLetteringOnMessageExpiration(state:ServiceBusSubscriptionConfig) = { state with DeadLetteringOnMessageExpiration = Some true }
    /// Adds filtering rules for a subscription
    [<CustomOperation "add_filters">]
    member _.AddFilters(state:ServiceBusSubscriptionConfig, filters) = { state with Rules = state.Rules @ filters }
    /// Adds a sql filtering rule for a subscription
    [<CustomOperation "add_sql_filter">]
    member this.AddFilter(state:ServiceBusSubscriptionConfig, name, expression) = this.AddFilters(state, [ Rule.CreateSqlFilter(name, expression) ])
    /// Adds a correlation filtering rule for a subscription
    [<CustomOperation "add_correlation_filter">]
    member this.AddCorrelationFilter(state:ServiceBusSubscriptionConfig, name, properties) = this.AddFilters(state, [ Rule.CreateCorrelationFilter(name, properties) ])
    
type ServiceBusTopicConfig =
    { Name : ResourceName
      DuplicateDetection : TimeSpan option
      DefaultMessageTimeToLive : TimeSpan option
      EnablePartitioning : bool option
      Subscriptions : Map<ResourceName, ServiceBusSubscriptionConfig> } with
        member this.BuildSubscriptions namespaceLinkedResource = [
            for subscription in this.Subscriptions do
                let subscription = subscription.Value
                { Name = subscription.Name
                  Namespace =
                        match namespaceLinkedResource with
                        | Managed resId
                        | Unmanaged resId -> resId.Name
                  Topic = this.Name
                  LockDuration = subscription.LockDuration |> Option.map IsoDateTime.OfTimeSpan
                  DuplicateDetectionHistoryTimeWindow = subscription.DuplicateDetection |> Option.map IsoDateTime.OfTimeSpan
                  DefaultMessageTimeToLive = subscription.DefaultMessageTimeToLive |> Option.map IsoDateTime.OfTimeSpan
                  MaxDeliveryCount = subscription.MaxDeliveryCount
                  Session = subscription.Session
                  DeadLetteringOnMessageExpiration = subscription.DeadLetteringOnMessageExpiration
                  Rules = subscription.Rules }
        ]
        member this.ResourceId = topics.resourceId this.Name
        member this.BuildResources location namespaceLinkedResource = [
            { Name = this.Name
              Dependencies = [
                match namespaceLinkedResource with
                | Managed resId -> resId // Only generate dependency if this is managed by Farmer (same template)
                | _ -> ()                  
              ] |> Set.ofList
              Namespace =
                match namespaceLinkedResource with
                | Managed resId
                | Unmanaged resId -> resId
              DuplicateDetectionHistoryTimeWindow = this.DuplicateDetection |> Option.map IsoDateTime.OfTimeSpan
              DefaultMessageTimeToLive = this.DefaultMessageTimeToLive |> Option.map IsoDateTime.OfTimeSpan
              EnablePartitioning = this.EnablePartitioning } :> IArmResource
            
            for subscription in this.BuildSubscriptions namespaceLinkedResource do
                subscription
        ]
        static member internal Empty =
            { Name = ResourceName.Empty
              DuplicateDetection = None
              DefaultMessageTimeToLive = None
              EnablePartitioning = None
              Subscriptions = Map.empty }
    
type ServiceBusTopicBuilder() =
    member _.Yield _ = ServiceBusTopicConfig.Empty
    /// The name of the queue.
    [<CustomOperation "name">] member _.Name(state:ServiceBusTopicConfig, name) = { state with Name = ResourceName name }
    /// Whether to enable duplicate detection, and if so, how long to check for.ServiceBusQueueConfig
    [<CustomOperation "duplicate_detection_minutes">] member _.DuplicateDetection(state:ServiceBusTopicConfig, maxTimeWindow) = { state with DuplicateDetection = Some (TimeSpan.FromMinutes (float maxTimeWindow)) }
    /// The default time-to-live for messages in a timespan string (e.g. '00:05:00'). If not specified, the maximum TTL will be set for the SKU.
    [<CustomOperation "message_ttl">]
    member _.MessageTtl(state:ServiceBusTopicConfig, ttl) = { state with DefaultMessageTimeToLive = Some (TimeSpan.Parse ttl) }
    /// The default time-to-live for messages in days. If not specified, the maximum TTL will be set for the SKU.
    member _.MessageTtl(state:ServiceBusTopicConfig, ttl:int<Days>) = { state with DefaultMessageTimeToLive = ttl / 1<Days> |> float |> TimeSpan.FromDays |> Some }
    /// Enables partition support on the queue.
    [<CustomOperation "enable_partition">] member _.EnablePartition(state:ServiceBusTopicConfig) = { state with EnablePartitioning = Some true }
    [<CustomOperation "add_subscriptions">]
    member _.AddSubscriptions(state:ServiceBusTopicConfig, subscriptions) =
        { state with
            Subscriptions =
                (state.Subscriptions, subscriptions)
                ||> List.fold(fun state (subscription:ServiceBusSubscriptionConfig) -> state.Add(subscription.Name, subscription))
        }

// needed to only expose IBuilder on a TopicConfig that has link_to_unmanaged_namespace specified
type ServiceBusTopicUnmanagedNamespaceConfig =
    { ServiceBusTopicConfig:ServiceBusTopicConfig
      Namespace : ResourceId }
    // This interface needs to exist on a different record because this interface existing on this recordType would allow a user to add a topic to an armTemplate without guaranteeing the namespace is either specified as an unmanaged or managed resource
    interface IBuilder with
        member this.ResourceId = this.ServiceBusTopicConfig.ResourceId
        member this.BuildResources location =
            this.ServiceBusTopicConfig.BuildResources location (Unmanaged this.Namespace)
type ServiceBusTopicUnmanagedNamespaceBuilder() =
    member _.Yield _ =
        { ServiceBusTopicConfig = ServiceBusTopicConfig.Empty
          Namespace = namespaces.resourceId ResourceName.Empty}
    [<CustomOperation "add_topic">]
    member _.AddTopic(state:ServiceBusTopicUnmanagedNamespaceConfig, topic) =
        { state with ServiceBusTopicConfig = topic }
    /// Instead of creating a or modifying a namespace, configure this topic to point to another unmanaged namespace instance.
    [<CustomOperation "link_to_unmanaged_namespace">]
    member this.LinkToUnmanagedNamespace (state:ServiceBusTopicUnmanagedNamespaceConfig, namespaceName) =
        { state with Namespace = namespaces.resourceId(ResourceName namespaceName) }
    member this.LinkToUnmanagedNamespace (state:ServiceBusTopicUnmanagedNamespaceConfig, namespaceName:ResourceName) =
        { state with Namespace = namespaces.resourceId namespaceName }
    member this.LinkToUnmanagedNamespace (state:ServiceBusTopicUnmanagedNamespaceConfig, resourceId) =
        { state with Namespace = resourceId }
    member _.Run (state:ServiceBusTopicUnmanagedNamespaceConfig) =
        match state.Namespace.Name with
        | EmptyResourceName -> failwith "Must be linked to unmanaged namespace"
        | _ -> state
    
type ServiceBusConfig =
    { Name : ResourceName
      Sku : Sku
      Dependencies : ResourceId Set
      Queues : Map<ResourceName, ServiceBusQueueConfig>
      Topics : Map<ResourceName, ServiceBusTopicConfig>
      Tags: Map<string,string>  }
    member private this.GetKeyPath property =
        let expr = $"listkeys(resourceId('Microsoft.ServiceBus/namespaces/authorizationRules', '{this.Name.Value}', 'RootManageSharedAccessKey'), '2017-04-01').{property}"
        ArmExpression.create(expr, namespaces.resourceId this.Name)
    member this.NamespaceDefaultConnectionString = this.GetKeyPath "primaryConnectionString"
    member this.DefaultSharedAccessPolicyPrimaryKey = this.GetKeyPath "primaryKey"
    interface IBuilder with
        member this.ResourceId = namespaces.resourceId this.Name
        member this.BuildResources location = [
            { Name = this.Name
              Location = location
              Sku = this.Sku
              Dependencies = this.Dependencies
              Tags = this.Tags  }

            for queue in this.Queues do
              let queue = queue.Value
              { Name = queue.Name
                Namespace = this.Name
                LockDuration = queue.LockDuration |> Option.map IsoDateTime.OfTimeSpan
                DuplicateDetectionHistoryTimeWindow = queue.DuplicateDetection |> Option.map IsoDateTime.OfTimeSpan
                Session = queue.Session
                DeadLetteringOnMessageExpiration = queue.DeadLetteringOnMessageExpiration
                DefaultMessageTimeToLive =
                    match queue.DefaultMessageTimeToLive, this.Sku with
                    | None, Basic -> TimeSpan.FromDays 14.
                    | None, (Standard | Premium _) -> TimeSpan.MaxValue
                    | Some ttl, _ -> ttl
                    |> IsoDateTime.OfTimeSpan
                MaxDeliveryCount = queue.MaxDeliveryCount
                EnablePartitioning = queue.EnablePartitioning }

            for topic in this.Topics do
                for topicResource in topic.Value.BuildResources location (Managed(namespaces.resourceId this.Name)) do
                    topicResource
        ]

type ServiceBusBuilder() =
    interface IDependable<ServiceBusConfig> with member _.Add state newDeps = { state with Dependencies = state.Dependencies + newDeps }
    member _.Yield _ =
        { Name = ResourceName.Empty
          Sku = Basic
          Queues = Map.empty
          Topics = Map.empty
          Dependencies = Set.empty
          Tags = Map.empty  }
    member _.Run (state:ServiceBusConfig) =
        let isBetween min max v = v >= min && v <= max
        for queue in state.Queues do
            let queue = queue.Value

            match queue.DuplicateDetection, state.Sku with
            | Some _, Basic -> failwith $"Duplicate Detection cannot be set when creating a queue using the Basic tier (queue '{queue.Name.Value}' fails this check)."
            | _ -> ()
            queue.LockDuration |> Option.iter(fun lockDuration -> if lockDuration > TimeSpan(0,5,0) then failwith "Lock duration name must not be more than 5 minutes.")

        state
    /// The name of the namespace that holds the queue.
    [<CustomOperation "name">]
    member _.NamespaceName(state:ServiceBusConfig, name) = { state with Name = ServiceBusValidation.ServiceBusName.Create(name).OkValue.ResourceName }
    /// The SKU of the namespace.
    [<CustomOperation "sku">]
    member _.Sku(state:ServiceBusConfig, sku) = { state with Sku = sku }

    [<CustomOperation "add_queues">]
    member _.AddQueues(state:ServiceBusConfig, queues) =
        { state with
            Queues =
                (state.Queues, queues)
                ||> List.fold(fun state (queue:ServiceBusQueueConfig) -> state.Add(queue.Name, queue))
        }
    [<CustomOperation "add_topics">]
    member _.AddTopics(state:ServiceBusConfig, topics) =
        { state with
            Topics =
                (state.Topics, topics)
                ||> List.fold(fun state (topic:ServiceBusTopicConfig) -> state.Add(topic.Name, topic))
        }
    interface ITaggable<ServiceBusConfig> with member _.Add state tags = { state with Tags = state.Tags |> Map.merge tags }

let serviceBus = ServiceBusBuilder()
let topic = ServiceBusTopicBuilder()
let queue = ServiceBusQueueBuilder()
let subscription = ServiceBusSubscriptionBuilder()
let topicUnmanagedNamespace = ServiceBusTopicUnmanagedNamespaceBuilder()