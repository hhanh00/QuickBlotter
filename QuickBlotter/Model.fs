module Model

open System
open System.Collections.Generic
open System.Diagnostics
open System.Reactive.Linq
open System.Reactive.Concurrency
open FSharp.Reactive

type Order = {
    Id: string
    Symbol: string
    Quantity: int
    User: string
}

type Symbol = {
    Id: string
    LastPrice: float
}

type OrderUpdate = 
    | Add of Order
    | Delete of string

type Update = 
    | OrderUpdate of OrderUpdate
    | SymbolUpdate of Symbol

type CombinedUpdate =
    | Add of Order * Symbol
    | Delete of string

let mutable orderBook = Map.empty<string, Order>
let mutable symbolBook = Map.empty<string, Symbol>
let mutable symbolToOrders = Map.empty<string, Set<string>>
let mutable combinedOrderBook = Map.empty<string, Order * Symbol>

let processOrderUpdate update =
    let result = 
        match update with
        | OrderUpdate.Add o -> 
            orderBook <- orderBook.Add(o.Id, o)
            let orders = if symbolToOrders.ContainsKey(o.Symbol) then symbolToOrders.Item(o.Symbol) else Set.empty<string>
            symbolToOrders <- symbolToOrders.Add(o.Symbol, orders.Add(o.Id))
            let symbol = if symbolBook.ContainsKey(o.Symbol) then symbolBook.Item(o.Symbol) else { Id = o.Symbol; LastPrice = nan }
            let combinedUpdate = (o, symbol)
            combinedOrderBook <- combinedOrderBook.Add(o.Id, combinedUpdate)
            [ CombinedUpdate.Add combinedUpdate ]
        | OrderUpdate.Delete oid -> 
            let order = orderBook.Item(oid)
            orderBook <- orderBook.Remove(oid)    
            if symbolToOrders.ContainsKey(order.Symbol) then
                let orders = symbolToOrders.Item(order.Symbol)
                symbolToOrders <- symbolToOrders.Add(order.Symbol, orders.Remove(oid))
                [ CombinedUpdate.Delete oid ]
            else
                []
#if EXTRADEBUG
    Debug.Print(sprintf "OB size = %d" orderBook.Count)
    Debug.Print(sprintf "Pushed %d events" result.Length)
#endif
    result

let processSymbolUpdate update = 
    symbolBook <- symbolBook.Add(update.Id, update)
    let orders = if symbolToOrders.ContainsKey(update.Id) then symbolToOrders.Item(update.Id) else Set.empty<string>
    let combinedUpdates = Set.toSeq orders |> Seq.map(fun oid -> 
        let order, symbol = combinedOrderBook.Item(oid)
        (oid, (order, update))
        )

    combinedUpdates |> Seq.iter(fun combinedUpdate -> combinedOrderBook <- combinedOrderBook.Add combinedUpdate)
    let result = combinedUpdates |> Seq.map(fun (_, update) -> CombinedUpdate.Add update) |> Seq.toList
#if EXTRADEBUG
    Debug.Print(sprintf "Pushed %d events" result.Length)
#endif
    result

let updateModel update = 
    match update with
    | OrderUpdate ou ->
        processOrderUpdate ou 
    | SymbolUpdate su ->
        processSymbolUpdate su 
    :> IEnumerable<CombinedUpdate>

let orderUpdateEvent = new Event<OrderUpdate>()
let symbolUpdateEvent = new Event<Symbol>()

let orderUpdateStream = orderUpdateEvent.Publish |> Observable.map(fun u -> OrderUpdate u)
let symbolUpdateStream = symbolUpdateEvent.Publish |> Observable.map(fun u -> SymbolUpdate u)
let updates = Observable.merge orderUpdateStream symbolUpdateStream

let modelStream = observe { yield! updates }
let bufferedModelStream = modelStream.ObserveOn(NewThreadScheduler.Default).SelectMany(updateModel).Buffer(TimeSpan.FromMilliseconds(500.0)).Publish()
let connection = bufferedModelStream.Connect() // To dispose on shutdown

let r = Random()
let randomSymbol = Seq.initInfinite (fun _ -> char(int 'A' + r.Next(26)) |> string)

let mockOrderUpdates () = 
    let r = Random()
    async {
        let ids = ref Array.empty<Order>
        while true do
            do! Async.Sleep(10)
            let choice = r.Next(10)
            match choice with
            | 0 -> 
                let c = ids.Value.Length
                if c > 0 then
                    let o = ids.Value.[0]
                    ids := Array.ofSeq(Seq.skip 1 !ids)
                    orderUpdateEvent.Trigger(OrderUpdate.Delete o.Id)
            | x when x < 5 -> 
                let order = { Id = System.Guid.NewGuid() |> string; Symbol = Seq.head randomSymbol; Quantity = r.Next(1000); User = "A" }
                orderUpdateEvent.Trigger(OrderUpdate.Add order)
                ids := Array.append !ids [|order|]
            | _ -> 
                let c = ids.Value.Length
                if c > 0 then
                    let o = { ids.Value.[r.Next(c)] with Quantity = r.Next(1000) }
                    orderUpdateEvent.Trigger(OrderUpdate.Add o)
    } |> Async.StartImmediate

let mockSymbolUpdates () =
    async {
        while true do
            do! Async.Sleep(10)
            let s = Seq.head randomSymbol
            let update = { Id = s; LastPrice = r.NextDouble() * 100.0 }
            symbolUpdateEvent.Trigger(update)
    } |> Async.StartImmediate
    
let start() = 
    mockOrderUpdates()
    mockSymbolUpdates()

type Model = 
    static member Start() = start()