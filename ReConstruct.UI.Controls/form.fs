namespace ReConstruct.UI.Controls

open System
open System.Windows
open System.Windows.Controls

open ReConstruct.Core.Patterns

open ReConstruct.UI.Core
open ReConstruct.UI.Core.UI

type Form<'E> =
    {
        Root: UIElement;
        OnValidate: Event<'E>;
    }

type Field = 
    {
        Element: FrameworkElement;
        isValidValue: unit -> bool;
        validate: bool -> unit;
    }

type ValueField<'V> = 
    {
        Value: unit -> 'V;
        Field: Field;
    }

type FieldBlock = FieldBlock of string*int*Field
type FieldGroup = FieldGroup of FieldBlock list

module Form =

    let toField element validate tryParse = 
        {
            Value = tryParse >> snd;
            Field = 
            {
                Element = element;
                isValidValue = tryParse >> fst;
                validate = validate;
            }
        }

    let ValidateStyle key (box: FrameworkElement) isValid = if isValid then box.Style <- style key else box.Style <- style (key + "-invalid")

    let ToMultilineBox (binding: Binding<'a>) = 
        let styleKey = "multiline-box"
        let box = TextBox(Text = binding.Value, Style = style styleKey)
        toField box (ValidateStyle styleKey box) (fun() -> box.Text |> binding.TryParse)

    let ToTextBox (binding: Binding<'a>) =
        let styleKey = binding.Key + "-box"
        let box = TextBox(Text = binding.Value, Style = style styleKey)
        toField box (ValidateStyle styleKey box) (fun() -> box.Text |> binding.TryParse)

    let ToComboBox binding =
        let styleKey = binding.Key + "-combo-box"
        let box = ComboBox(Style = style styleKey)
        binding.Options |> List.iter(fun i -> box.Items.Add(i) |> ignore)
        box.SelectedItem <- binding.Selected
        toField box (ValidateStyle styleKey box) (fun() -> (true, box.SelectedIndex |> binding.value))

    let ToCheckBox value =
        let styleKey = "check-box"
        let box = CheckBox(Style = style styleKey, IsChecked = Nullable(value))
        toField box (ValidateStyle styleKey box) (fun() -> (true, box.IsChecked.GetValueOrDefault()))

    let Form (fieldGroups: FieldGroup list) =

        let onValidate = Event<_>()

        let withValidator field =
            field.Element.LostFocus |> Event.add(fun _ -> field.isValidValue() |> branch(onValidate.Trigger) |> field.validate)
            field.Element

        let wrapInBlock label width element =
            let row = StackPanel(Style = style "field", Width = float width)
            let caption = label |> textBlock "caption-text"
            caption >- row
            element >- row
            row :> UIElement

        let fieldBlock fieldBlock =
            let (FieldBlock(label, width, field)) = fieldBlock
            field |> withValidator |> wrapInBlock label width

        let fieldGroup (fieldGroup) =
            let (FieldGroup(blocks)) = fieldGroup
            stack "fields-group" |> branch(fun group -> blocks |> List.iter(fun b -> b |> fieldBlock >- group))

        {
            Root = stack "form" |> branch(fun root -> fieldGroups |> List.iter(fun g -> g |> fieldGroup >- root))
            OnValidate = onValidate;
        }
