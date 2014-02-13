namespace ViewModel

open Model
open System.ComponentModel
open System.Reactive.Linq
open System.Reactive.Concurrency
open System.Diagnostics
open System.Threading

type IBatchUpdate =
    interface
        abstract BeginUpdate: unit -> unit
        abstract EndUpdate: unit -> unit
    end

type CombinedOrder = {
    Order: Order;
    Symbol: Symbol;
    }   
    with 
    member x.Id with get() = x.Order.Id
    member x.Code with get() = x.Symbol.Id
    member x.Price with get() = x.Symbol.LastPrice

type OrderBlotter() as self =
    inherit BindingList<CombinedOrder>()

    let mutable view = { 
        new IBatchUpdate with
            member x.BeginUpdate() = ignore() 
            member x.EndUpdate() = ignore() 
        }
    let modelStream = Model.bufferedModelStream
    let mutable indices = Map.empty<string, Ref<int>>

    do
        modelStream.ObserveOnDispatcher().Subscribe(fun us ->
            Debug.Print(sprintf "%d> Received %d changes" Thread.CurrentThread.ManagedThreadId us.Count)
            view.BeginUpdate()
            for u in us do
                assert (indices.Count = self.Count)
                match u with
                | CombinedUpdate.Add (order, symbol) -> 
                    if indices.ContainsKey(order.Id) then
                        let index = !indices.Item(order.Id)
                        self.Item(index) <- { Order = order; Symbol = symbol }
                    else
                        let index = indices.Count
                        indices <- indices.Add(order.Id, ref index)
                        self.Add({ Order = order; Symbol = symbol })
                | CombinedUpdate.Delete oid -> 
                    if indices.ContainsKey(oid) then
                        let index = !indices.Item(oid)
                        self.RemoveAt(index) |> ignore
                        indices <- indices.Remove(oid)
                        indices |> Map.map(fun k v -> if !v > index then v := !v - 1) |> ignore
            view.EndUpdate()
        ) |> ignore

    member x.SetView (v: IBatchUpdate) = view <- v