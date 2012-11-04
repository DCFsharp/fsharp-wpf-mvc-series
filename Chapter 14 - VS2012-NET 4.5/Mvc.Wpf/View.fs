﻿namespace Mvc.Wpf

open System
open System.Windows
open System.Windows.Controls

type IPartialView<'Event, 'Model> = 
    inherit IObservable<'Event>

    abstract SetBindings : 'Model -> unit

type IView<'Event, 'Model> =
    inherit IPartialView<'Event, 'Model>

    abstract ShowDialog : unit -> bool
    abstract Show : unit -> Async<bool>
    abstract Close : bool -> unit

[<AbstractClass>]
type PartialView<'Event, 'Model, 'Control when 'Control :> FrameworkElement>(control : 'Control) =

    member this.Control = control
    static member (?) (view : PartialView<'Event, 'Model, 'Window>, name) = 
        match view.Control.FindName name with
        | null -> 
            match view.Control.TryFindResource name with
            | null -> invalidArg "Name" ("Cannot find child control or resource named: " + name)
            | resource -> resource |> unbox
        | control -> control |> unbox
    
    interface IPartialView<'Event, 'Model> with
        member this.Subscribe observer = 
            let xs = this.EventStreams |> List.reduce Observable.merge 
            xs.Subscribe observer
        member this.SetBindings model = 
            control.DataContext <- model; 
            this.SetBindings model

    abstract EventStreams : IObservable<'Event> list
    abstract SetBindings : 'Model -> unit

[<AbstractClass>]
type View<'Event, 'Model, 'Window when 'Window :> Window and 'Window : (new : unit -> 'Window)>(?window) = 
    inherit PartialView<'Event, 'Model, 'Window>(control = defaultArg window (new 'Window()))

    let mutable isOK = false

    interface IView<'Event, 'Model> with
        member this.ShowDialog() = 
            this.Control.ShowDialog() |> ignore
            isOK
        member this.Show() = 
            this.Control.Show()
            this.Control.Closed |> Event.map (fun _ -> isOK) |> Async.AwaitEvent 
        member this.Close isOK' = 
            isOK <- isOK'
            this.Control.Close()

[<AbstractClass>]
type XamlView<'Event, 'Model>(resourceLocator) = 
    inherit View<'Event, 'Model, Window>(resourceLocator |> Application.LoadComponent |> unbox)

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module View = 

    type IView<'Event, 'Model> with

        member this.OK() = this.Close true
        member this.Cancel() = this.Close false

        member this.CancelButton with set(value : Button) = value.Click.Add(ignore >> this.Cancel)
        member this.DefaultOKButton 
            with set(value : Button) = 
                value.IsDefault <- true
                value.Click.Add(ignore >> this.OK)

        member parent.Compose(child : IPartialView<_, 'MX>, childModelSelector : _ -> 'MX ) =
            {
                new IView<_, _> with
                    member __.Subscribe observer = (Observable.unify parent child).Subscribe(observer)
                    member __.SetBindings model =
                        parent.SetBindings model  
                        model |> childModelSelector |> child.SetBindings
                    member __.Show() = parent.Show()
                    member __.ShowDialog() = parent.ShowDialog()
                    member __.Close ok = parent.Close ok
            }

        member view.Compose extension =
            {
                new IView<_, _> with
                    member __.Subscribe observer = (Observable.unify view extension).Subscribe(observer)
                    member __.SetBindings model = view.SetBindings model  
                    member __.Show() = view.Show()
                    member __.ShowDialog() = view.ShowDialog()
                    member __.Close ok = view.Close ok
            }

[<RequireQualifiedAccess>]
module List =
    open System.Windows.Controls

    let ofButtonClicks xs = xs |> List.map(fun(b : Button, value) -> b.Click |> Observable.mapTo value)
    
    