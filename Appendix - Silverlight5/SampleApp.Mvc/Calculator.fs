﻿namespace SampleApp

open System
open System.Windows.Data
open Microsoft.FSharp.Reflection
open FSharp.Windows

type Operations =
    | Add
    | Subtract
    | Multiply
    | Divide

    override this.ToString() = sprintf "%A" this

    static member Values = 
        typeof<Operations>
        |> FSharpType.GetUnionCases
        |> Array.map(fun x -> FSharpValue.MakeUnion(x, [||]))
        |> Array.map unbox<Operations>

[<AbstractClass>]
type CalculatorModel() = 
    inherit Model()

    abstract AvailableOperations : Operations[] with get, set
    abstract SelectedOperation : Operations with get, set
    abstract X : int with get, set
    abstract Y : int with get, set
    abstract Result : int with get, set

type CalculatorEvents = 
    | Calculate
    | Clear 
    | Hex1
    | Hex2
    | YChanged of string

type CalculatorView(control) =
    inherit View<CalculatorEvents, CalculatorModel, CalculatorControl>(control)

    override this.EventStreams = 
        [ 
            yield! [
                this.Control.Calculate, Calculate
                this.Control.Clear, Clear
                this.Control.Hex1, Hex1
                this.Control.Hex2, Hex2
            ]
            |> List.ofButtonClicks
                 
            yield this.Control.Y.TextChanged |> Observable.map(fun _ -> YChanged(this.Control.Y.Text))
        ] 

    override this.SetBindings model = 
        Binding.FromExpression 
            <@ 
                this.Control.Operation.ItemsSource <- model.AvailableOperations 
                this.Control.Operation.SelectedItem <- model.SelectedOperation
                this.Control.Result.Text <- string model.Result 
            @>
            
        Binding.TwoWay
            <@ 
                this.Control.X.Text <- string model.X
                this.Control.Y.Text <- string model.Y 
            @>

type CalculatorController() = 
    inherit Controller<CalculatorEvents, CalculatorModel>()

    override this.InitModel model = 
        model.AvailableOperations <- Operations.Values |> Array.filter(fun op -> op <> Operations.Divide)
        model.SelectedOperation <- Operations.Add
        model.X <- 0
        model.Y <- 0
        model.Result <- 0

    override this.Dispatcher = function
        | Calculate -> Sync this.Calculate
        | Clear -> Sync this.InitModel
        | Hex1 -> Async this.Hex1
        | Hex2 -> Sync this.Hex2
        | YChanged text -> Sync(this.YChanged text)

    member this.Calculate model = 
        model.ClearAllErrors()
        match model.SelectedOperation with
        | Add -> 
            model |> Validation.positive <@ fun m -> m.Y @>
            if not model.HasErrors
            then 
                model.Result <- model.X + model.Y
        | Subtract -> 
            model |> Validation.positive <@ fun m -> m.Y @>
            if not model.HasErrors
            then 
                model.Result <- model.X - model.Y
        | Multiply -> 
            model.Result <- model.X * model.Y
        | Divide -> 
            if model.Y = 0 
            then
                model |> Validation.setError <@ fun m -> m.Y @> "Attempted to divide by zero."
            else
                model.Result <- model.X / model.Y
        
    member this.Hex1 model =
        let view = HexConverter.view()
        let childModel : HexConverter.Model = Model.Create() 
        let controller = HexConverter.controller() 
        childModel.Value <- model.X

        async {
            let! result = Mvc.StartDialog(childModel, view, controller)
            if result then model.X <- childModel.Value 
        }

    member this.Hex2 model = ()
//        (HexConverter.view(), HexConverter.controller())
//        |> Mvc.startDialog
//        |> Option.iter(fun resultModel ->
//            model.Y <- resultModel.Value 
//        )

    member this.YChanged text model = 
        if text <> "0"
        then 
            model.AvailableOperations <- Operations.Values
        else 
            model.AvailableOperations <- Operations.Values |> Array.filter(fun op -> op <> Operations.Divide)

//    member this.EnsureDigitalInput(newValue, cancel) model =
//        match Int32.TryParse newValue with 
//        | false, _  ->  cancel()
//        | _ -> ()
