namespace ReConstruct.UI.Core

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Controls.Primitives

module UI =

    let style name = Application.Current.TryFindResource(name) :?> Style

    let asUIElement c = c :> UIElement

    let navigationContainer = Frame(Style = style "MainFrame")

    let window (content: UIElement) = Window (
                                            Content = content, 
                                            Visibility = Visibility.Visible, 
                                            WindowState = WindowState.Maximized, 
                                            WindowStyle = WindowStyle.None,
                                            Style = style "DefaultWindow"
                                        )
    let disable<'T when 'T :> UIElement> (control: 'T) = control.IsEnabled <- false; control;
    let enable<'T when 'T :> UIElement> (control: 'T) = control.IsEnabled <- true; control;

    let enableIf = function 
                    | true  -> enable
                    | false -> fun b -> b


    let label content = Label(Content = content, Style = style "Label")

    let textBlock styleKey text = TextBlock(Text = text, Style = style styleKey)

    let textBox text = TextBox(Text = text, Style = style "text-box")
    
    let customText text styleKey = TextBlock(Text = text, Style = style styleKey)

    let button content = Button(Content = content, Style = style "base-button")

    let iconButton content = Button(Content = content, Style = style "icon-button")

    let stack styleKey = StackPanel(Style = style styleKey)

    let grid styleKey = Grid(Style = style styleKey)

    let border styleKey = Border(Style = style styleKey)

    let withStyle styleKey (control: FrameworkElement) = control.Style <- style styleKey

    let withIcon icon (button: Button) = button.Content <- icon

    let listBox styleKey = ListBox(Style = style styleKey)

    let uniformGrid() = UniformGrid()

    let loadContent (panel: Panel) content =
        panel.Children.Clear()
        panel.Children.Add(content) |> ignore

    let customButton content styleKey onClick = 
        let button = Button(Content = content, Style = style styleKey)
        button.Click.Add(fun e -> onClick e) |> ignore
        button

    let separator() = Separator(Style = style "Separator")

    let treeView styleKey = new TreeView(Style = style styleKey)
    let treeViewItem styleKey text = new TreeViewItem(Style = style styleKey, Header = text)
    let listViewItem text = new ListViewItem(Content = text)
    let listBoxItem text = new ListBoxItem(Content = text)

    let (>-) child (parent:Panel) = parent.Children.Add(child) |> ignore

    let childOf (parent:Panel) child = parent.Children.Add(child) |> ignore

    let onlyChildOf (parent:Decorator) child = parent.Child <- child

    let childrenOf (parent:Panel) (children: UIElement seq) =
        children |> Seq.map(fun child -> parent.Children.Add(child)) |> ignore
        parent

    let withChild child (parent:Panel) = 
        parent.Children.Add(child) |> ignore
        parent

    let withParent child (parent:Border) = 
        parent.Child <- child
        parent

    let addItemTo (parent: ItemsControl) child = parent.Items.Add(child) |> ignore

    let dockTo (parent:DockPanel) position child = 
        parent.Children.Add(child) |> ignore
        DockPanel.SetDock(child, position)

    let withClick onClick (button:Button) = 
        button.Click |> Event.add(fun e -> onClick button)
        button

    let whenChildIs (apply: 'T -> unit) (children: UIElementCollection) = [0 .. children.Count - 1] 
                                                                                |> Seq.choose (fun i -> if children.Item(i) :? 'T then Some (children.Item(i) :?> 'T) else None) 
                                                                                |> Seq.iter(fun b -> apply b)
    let withFirstOf (apply: 'T -> unit) (children: UIElementCollection) = 
        let first = [0 .. children.Count - 1] |> Seq.tryPick(fun i -> if children.Item(i) :? 'T then Some (children.Item(i) :?> 'T) else None) 
        match first with 
            | Some c -> apply c
            | None -> ()

    let collapse (element: UIElement) = element.Visibility <- Visibility.Collapsed
    let show (element: UIElement) = element.Visibility <- Visibility.Visible

    let alignRight(element: FrameworkElement)  =
        element.HorizontalAlignment <- HorizontalAlignment.Right
        element

    let alignLeft(element: FrameworkElement)  =
        element.HorizontalAlignment <- HorizontalAlignment.Left
        element