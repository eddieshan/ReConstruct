namespace ReConstruct.UI.Controls

open System
open System.Windows.Controls

open ReConstruct.UI.Core.UI

module TreePager = 

    let New displayPage dataPages = 

        let selectPageEvent = Event<_>()
        let selectGroupEvent = Event<_>()
        let goToPageEvent = Event<_>()

        let pageButton caption (parentMarker: Button Option) index data = 
            let button = Button(Content = caption, Style = style "page-button")

            let onPageSelected() = 
                selectPageEvent.Trigger()
                button.Style <- style "page-button-selected"
                displayPage data
                match parentMarker with | Some(b) -> b.Style <- style "page-marker-selected" | None -> ()

            selectPageEvent.Publish |> Event.add(fun e -> button.Style <- style "page-button")
            button.Click |> Event.add(fun _ -> onPageSelected())

            goToPageEvent.Publish |> Event.filter(fun i -> i = index) |> Event.add(fun i -> onPageSelected())

            button

        let selectGroup(marker: Button, buttonsContainer) = 
            selectGroupEvent.Trigger()
            buttonsContainer |> show

        let groupOfPages caption =
            let buttons = stack "page-group-buttons"
            let group = stack "page-group"
            let marker = Button(Content = caption, Style = style "page-marker")
                                    
            marker.Click |> Event.add(fun e -> selectGroup(marker, buttons)) 

            marker >- group
            buttons >- group

            selectGroupEvent.Publish |> Event.add(fun _ -> buttons |> collapse)
            selectPageEvent.Publish |> Event.add(fun _ -> marker.Style <- style "page-marker")

            buttons |> collapse

            (group, marker, buttons)
            

        let rec buildPager index parent parentMarker dataPages = 
            match dataPages with
                | [] -> index
                | Page(data, caption) :: tail -> 
                                    data |> pageButton caption parentMarker index >- parent
                                    tail |> buildPager (index + 1) parent parentMarker

                | Marker(caption, pages ):: tail -> 
                                    let group, marker, buttons = caption |> groupOfPages

                                    group >- parent
                                    
                                    let newIndex = pages |> buildPager index buttons (Some marker)
                                    tail |> buildPager newIndex parent parentMarker                                    

        let pager = stack "pager-panel"
        dataPages |> buildPager 0 pager None |> ignore

        goToPageEvent.Trigger(0)
        pager