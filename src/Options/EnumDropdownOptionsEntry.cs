using System;
using System.Collections.Generic;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using UnityEngine;

namespace RevealStarMap.Options;

internal abstract class EnumDropdownOptionsEntry<TEnum> : OptionsEntry where TEnum : struct, Enum
{
    protected sealed class EnumChoice : IListableOption, ITooltipListableOption
    {
        internal EnumChoice(string title, string tooltip, TEnum value)
        {
            Title = title;
            Tooltip = tooltip;
            Value = value;
        }

        internal string Title { get; }

        internal string Tooltip { get; }

        internal TEnum Value { get; }

        public string GetProperName()
        {
            return Title;
        }

        public string GetToolTipText()
        {
            return Tooltip;
        }
    }

    private readonly IList<EnumChoice> choices;
    private readonly int widestLabel;
    private EnumChoice? selected;
    private GameObject? comboBox;

    public override object? Value
    {
        get => selected?.Value;
        set
        {
            selected = ResolveChoice(value);
            Refresh();
        }
    }

    protected EnumDropdownOptionsEntry(string field, string title, string tooltip, string category, IEnumerable<(string Title, string Tooltip, TEnum Value)> options)
        : base(field, new OptionAttribute(title, tooltip, category))
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var builtChoices = new List<EnumChoice>();
        int longest = 1;
        foreach ((string optionTitle, string optionTooltip, TEnum optionValue) in options)
        {
            string resolvedTitle = string.IsNullOrWhiteSpace(optionTitle) ? optionValue.ToString() : optionTitle;
            builtChoices.Add(new EnumChoice(resolvedTitle, optionTooltip ?? string.Empty, optionValue));
            longest = Math.Max(longest, resolvedTitle.Trim().Length);
        }

        if (builtChoices.Count == 0)
        {
            throw new ArgumentException("At least one enum choice is required.", nameof(options));
        }

        choices = builtChoices;
        widestLabel = longest;
        selected = builtChoices[0];
    }

    public override GameObject GetUIComponent()
    {
        comboBox = new PComboBox<EnumChoice>("Select")
        {
            BackColor = PUITuning.Colors.ButtonPinkStyle,
            InitialItem = selected ?? choices[0],
            Content = choices,
            EntryColor = PUITuning.Colors.ButtonBlueStyle,
            TextStyle = PUITuning.Fonts.TextLightStyle,
            OnOptionSelected = (_, choice) =>
            {
                if (choice != null)
                {
                    selected = choice;
                }
            }
        }.SetMinWidthInCharacters(widestLabel).Build();

        Refresh();
        return comboBox;
    }

    private EnumChoice ResolveChoice(object? rawValue)
    {
        if (rawValue is TEnum typedValue)
        {
            foreach (EnumChoice choice in choices)
            {
                if (EqualityComparer<TEnum>.Default.Equals(choice.Value, typedValue))
                {
                    return choice;
                }
            }
        }

        if (rawValue != null)
        {
            string text = rawValue.ToString() ?? string.Empty;
            foreach (EnumChoice choice in choices)
            {
                if (string.Equals(choice.Value.ToString(), text, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(choice.Title, text, StringComparison.OrdinalIgnoreCase))
                {
                    return choice;
                }
            }

            if (int.TryParse(text, out int intValue) && Enum.IsDefined(typeof(TEnum), intValue))
            {
                TEnum parsed = (TEnum)Enum.ToObject(typeof(TEnum), intValue);
                foreach (EnumChoice choice in choices)
                {
                    if (EqualityComparer<TEnum>.Default.Equals(choice.Value, parsed))
                    {
                        return choice;
                    }
                }
            }
        }

        return choices[0];
    }

    private void Refresh()
    {
        if (comboBox != null && selected != null)
        {
            PComboBox<EnumChoice>.SetSelectedItem(comboBox, selected);
        }
    }
}
