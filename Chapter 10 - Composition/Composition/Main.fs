﻿namespace Mvc.Wpf.Sample

open System
open System.Diagnostics
open System.Windows.Data
open System.Windows
open System.Windows.Controls
open Mvc.Wpf
open Mvc.Wpf.UIElements

[<AbstractClass>]
type MainModel() = 
    inherit Model()

    abstract Calculator : CalculatorModel with get, set
    abstract TempConveter : TempConveterModel with get, set
    abstract StockPricesChart : StockPricesChartModel with get, set

    abstract ProcessName : string with get, set
    abstract ActiveTab : string with get, set

type MainEvents = ActiveTabChanged of string

type MainView() = 
    inherit View<MainEvents, MainModel, MainWindow>()

    override this.EventStreams = 
        [   
            this.Control.Tabs.SelectionChanged |> Observable.map(fun _ -> 
                let activeTab : TabItem = unbox this.Control.Tabs.SelectedItem
                let header = string activeTab.Header
                ActiveTabChanged header)
        ]

    override this.SetBindings model = 
        let titleBinding = MultiBinding(StringFormat = "{0} - {1}")
        titleBinding.Bindings.Add <| Binding("ProcessName")
        titleBinding.Bindings.Add <| Binding("ActiveTab")
        this.Control.SetBinding(Window.TitleProperty, titleBinding) |> ignore

type MainController(view) = 
    inherit SupervisingController<MainEvents, MainModel>(view)

    override this.InitModel model = 
        model.ProcessName <- Process.GetCurrentProcess().ProcessName
        model.ActiveTab <- "Calculator"

        model.Calculator <- Model.Create()
        model.TempConveter <- Model.Create()
        model.StockPricesChart <- Model.Create()

    override this.Dispatcher = Sync << function
        | ActiveTabChanged header -> this.ActiveTabChanged header

    member this.ActiveTabChanged header model =
        model.ActiveTab <- header