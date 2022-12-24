﻿/*
Copyright (c) 2018-2019 Festo AG & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AasCore.Aas3_0_RC02;
using AasxIntegrationBase;
using AdminShellNS;
using AnyUi;
using Extensions;
using Newtonsoft.Json;

namespace AasxPackageLogic
{
    /// <summary>
    /// This class extends the basic helper functionalities of DispEditHelper by providing modules for display/
    /// editing disting modules of the GUI, such as the different (re-usable) Interfaces of the AAS entities
    /// </summary>
    public class DispEditHelperMiniModules : DispEditHelperCopyPaste
    {
        //
        // Inject a number of functions bigger than basics but smaller than modules
        //
        // In particular, some helper for lists of Qualifiers, IdentifierKeyValuePairs, Extensions
        // are provided.
        // Note: In theorey, these 3 functions have VERY SIMILARY structure, but are not unified to
        //       a generic one (yet), as the ratio between UI/glue logic and business logic per function
        //       is quite high.
        //

        //
        // Qualifiers
        //

        private bool PasteQualifierTextIntoExisting(
            string jsonInput,
            Qualifier qCurr)
        {
            var qIn = JsonConvert.DeserializeObject<Qualifier>(jsonInput);
            if (qCurr != null && qIn != null)
            {
                qCurr.Type = qIn.Type;
                qCurr.Value = qIn.Value;
                qCurr.ValueType = qIn.ValueType;
                if (qIn.ValueId != null)
                    qCurr.ValueId = qIn.ValueId;
                if (qIn.SemanticId != null)
                    qCurr.SemanticId = qIn.SemanticId;
                Log.Singleton.Info("Qualifier data taken from clipboard.");
                return true;
            }
            return false;
        }

        // ReSharper Disable once ClassNeverInstantiated.Global

        public class QualifierPreset
        {
            public string name = "";
            public Qualifier qualifier = new Qualifier("", DataTypeDefXsd.String);
        }

        public void QualifierHelper(
            AnyUiStackPanel stack, ModifyRepo repo,
            List<Qualifier> qualifiers,
            IReferable relatedReferable = null)
        {
            if (editMode)
            {
                // let the user control the number of references
                AddAction(
                    stack, "Qualifier entities:",
                    new[] { "Add blank", "Add preset", "Add from clipboard", "Delete last" },
                    repo,
                    (buttonNdx) =>
                    {
                        if (buttonNdx == 0)
                        {
                            qualifiers.Add(new Qualifier("", DataTypeDefXsd.String));
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        }

                        if (buttonNdx == 1)
                        {
                            var pfn = Options.Curr.QualifiersFile;
                            if (pfn == null || !System.IO.File.Exists(pfn))
                            {
                                Log.Singleton.Error(
                                    $"JSON file for Quialifer presets not defined nor existing ({pfn}).");
                                return new AnyUiLambdaActionNone();
                            }
                            try
                            {
                                // read file contents
                                var init = System.IO.File.ReadAllText(pfn);
                                var presets = JsonConvert.DeserializeObject<List<QualifierPreset>>(init);

                                // define dialogue and map presets into dialogue items
                                var uc = new AnyUiDialogueDataSelectFromList();
                                uc.ListOfItems = presets.Select((pr)
                                        => new AnyUiDialogueListItem() { Text = pr.name, Tag = pr }).ToList();

                                // perform dialogue
                                this.context.StartFlyoverModal(uc);
                                if (uc.Result && uc.ResultItem?.Tag is QualifierPreset preset
                                    && preset.qualifier != null)
                                {
                                    qualifiers.Add(preset.qualifier);
                                    this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Singleton.Error(
                                    ex, $"While show Qualifier presets ({pfn})");
                            }
                        }

                        if (buttonNdx == 2)
                        {
                            try
                            {
                                var qNew = new Qualifier("", DataTypeDefXsd.String);
                                var jsonInput = this.context?.ClipboardGet()?.Text;
                                if (PasteQualifierTextIntoExisting(jsonInput, qNew))
                                {
                                    qualifiers.Add(qNew);
                                    this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Singleton.Error(ex, "while accessing Qualifier data in clipboard");
                            }
                        }

                        if (buttonNdx == 3 && qualifiers.Count > 0)
                            qualifiers.RemoveAt(qualifiers.Count - 1);

                        return new AnyUiLambdaActionRedrawEntity();
                    });
            }

            for (int i = 0; i < qualifiers.Count; i++)
            {
                var qual = qualifiers[i];
                var substack = AddSubStackPanel(stack, "  "); // just a bit spacing to the left

                int storedI = i;
                AddGroup(
                    substack, $"Qualifier {1 + i}",
                    levelColors.SubSubSection.Bg, levelColors.SubSubSection.Fg, repo,
                    contextMenuText: "\u22ee",
                    menuHeaders: new[] {
                        "\u2702", "Delete",
                        "\u25b2", "Move Up",
                        "\u25bc", "Move Down",
                        "\u29c9", "Copy to clipboard",
                        "\u2398", "Paste from clipboard",
                    },
                    menuItemLambda: (o) =>
                    {
                        var action = false;

                        if (o is int ti)
                            switch (ti)
                            {
                                case 0:
                                    qualifiers.Remove(qual);
                                    action = true;
                                    break;
                                case 1:
                                    var resu = this.MoveElementInListUpwards<Qualifier>(
                                        qualifiers, qualifiers[storedI]);
                                    if (resu > -1)
                                    {
                                        action = true;
                                    }
                                    break;
                                case 2:
                                    var resd = this.MoveElementInListDownwards<Qualifier>(
                                        qualifiers, qualifiers[storedI]);
                                    if (resd > -1)
                                    {
                                        action = true;
                                    }
                                    break;
                                case 3:
                                    var jsonStr = JsonConvert.SerializeObject(
                                        qualifiers[storedI], Formatting.Indented);
                                    this.context?.ClipboardSet(new AnyUiClipboardData(jsonStr));
                                    Log.Singleton.Info("Qualified serialized to clipboard.");
                                    break;
                                case 4:
                                    try
                                    {
                                        var jsonInput = this.context?.ClipboardGet()?.Text;
                                        action = PasteQualifierTextIntoExisting(jsonInput, qualifiers[storedI]);
                                        if (action)
                                            Log.Singleton.Info("Qualifier taken from clipboard.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Singleton.Error(ex, "while accessing Qualifier data in clipboard");
                                    }
                                    break;

                            }

                        if (action)
                        {
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }
                        return new AnyUiLambdaActionNone();
                    },
                    margin: new AnyUiThickness(2, 2, 2, 2),
                    padding: new AnyUiThickness(5, 0, 5, 0));

                AddHintBubble(
                    substack, hintMode,
                    new[] {
                        new HintCheck(
                            () => {
                                return (qual.SemanticId == null || qual.SemanticId.IsEmpty()) &&
                                    (qual.Type == null || qual.Type.Trim() == "");
                            },
                            "Either a semanticId or a type string specification shall be given!")
                    });
                if (SafeguardAccess(
                        substack, repo, qual.SemanticId, "semanticId:", "Create data element!",
                        v =>
                        {
                            qual.SemanticId = new Reference(ReferenceTypes.GlobalReference, new List<Key>());
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }))
                {
                    List<string> keys = new();
                    foreach (var key in qual.SemanticId.Keys)
                    {
                        keys.Add(key.Value);
                    }
                    AddKeyListOfIdentifier(
                        substack, "semanticId", keys, repo,
                        packages, PackageCentral.PackageCentral.Selector.MainAuxFileRepo,
                        addExistingEntities: "All",
                        addEclassIrdi: true,
                        relatedReferable: relatedReferable);
                }

                AddKeyValueExRef(
                    substack, "type", qual, qual.Type, null, repo,
                    v =>
                    {
                        qual.Type = v as string;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    });

                AddKeyValueExRef(
                    substack, "value", qual, qual.Value, null, repo,
                    v =>
                    {
                        qual.Value = v as string;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    });

                if (SafeguardAccess(
                        substack, repo, qual.ValueId, "valueId:", "Create data element!",
                        v =>
                        {
                            qual.ValueId = new Reference(ReferenceTypes.GlobalReference, new List<Key>());
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }))
                {
                    List<string> keys = new();
                    foreach (var key in qual.ValueId.Keys)
                    {
                        keys.Add(key.Value);
                    }
                    AddKeyListOfIdentifier(substack, "valueId", keys, repo,
                        packages, PackageCentral.PackageCentral.Selector.MainAuxFileRepo, "All",
                        relatedReferable: relatedReferable);
                }

            }

        }

        //
        // Key Value Pairs
        //

        private bool PasteIKVPTextIntoExisting(
            string jsonInput,
            SpecificAssetId qCurr)
        {
            var qIn = JsonConvert.DeserializeObject<SpecificAssetId>(jsonInput);
            if (qCurr != null && qIn != null)
            {
                qCurr.Name = qIn.Name;
                qCurr.Value = qIn.Value;
                if (qIn.ExternalSubjectId != null)
                    qCurr.ExternalSubjectId = qIn.ExternalSubjectId;
                if (qIn.SemanticId != null)
                    qCurr.SemanticId = qIn.SemanticId;
                Log.Singleton.Info("SpecificAssetId data taken from clipboard.");
                return true;
            }
            return false;
        }

        // ReSharper Disable once ClassNeverInstantiated.Global

        /// <summary>
        /// This class defines the JSON format for presets.
        /// </summary>
        public class IdentifierKeyValuePairPreset
        {
            public string name = "";
            public SpecificAssetId pair = new("", "", null);
        }

        public void IdentifierKeyValuePairHelper(
            AnyUiStackPanel stack, ModifyRepo repo,
            List<SpecificAssetId> pairs,
            string key = "IdentifierKeyValuePairs",
            IReferable relatedReferable = null)
        {
            if (editMode)
            {
                // let the user control the number of references
                AddAction(
                    stack, $"{key}:",
                    new[] { "Add blank", "Add preset", "Add from clipboard", "Delete last" },
                    repo,
                    (buttonNdx) =>
                    {
                        if (buttonNdx == 0)
                        {
                            pairs.Add(new SpecificAssetId("", "", null));
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        }

                        if (buttonNdx == 1)
                        {
                            var pfn = Options.Curr.IdentifierKeyValuePairsFile;
                            if (pfn == null || !System.IO.File.Exists(pfn))
                            {
                                Log.Singleton.Error(
                                    "JSON file for SpecificAssetId presets not defined nor existing ({pfn}).");
                                return new AnyUiLambdaActionNone();
                            }
                            try
                            {
                                // read file contents
                                var init = System.IO.File.ReadAllText(pfn);
                                var presets = JsonConvert.DeserializeObject<List<IdentifierKeyValuePairPreset>>(init);

                                // define dialogue and map presets into dialogue items
                                var uc = new AnyUiDialogueDataSelectFromList();
                                uc.ListOfItems = presets.Select((pr)
                                        => new AnyUiDialogueListItem() { Text = pr.name, Tag = pr }).ToList();

                                // perform dialogue
                                this.context.StartFlyoverModal(uc);
                                if (uc.Result && uc.ResultItem?.Tag is IdentifierKeyValuePairPreset preset
                                    && preset.pair != null)
                                {
                                    pairs.Add(preset.pair);
                                    this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Singleton.Error(
                                    ex, $"While show SpecificAssetId presets ({pfn})");
                            }
                        }

                        if (buttonNdx == 2)
                        {
                            try
                            {
                                var pNew = new SpecificAssetId("", "", null);
                                var jsonInput = this.context?.ClipboardGet()?.Text;
                                if (PasteIKVPTextIntoExisting(jsonInput, pNew))
                                {
                                    pairs.Add(pNew);
                                    this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Singleton.Error(ex, "while accessing SpecificAssetId data in clipboard");
                            }
                        }

                        if (buttonNdx == 3 && pairs.Count > 0)
                            pairs.RemoveAt(pairs.Count - 1);

                        return new AnyUiLambdaActionRedrawEntity();
                    });
            }

            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i];
                var substack = AddSubStackPanel(stack, "  "); // just a bit spacing to the left

                int storedI = i;
                AddGroup(
                    substack, $"Pair {1 + i}",
                    levelColors.SubSubSection.Bg, levelColors.SubSubSection.Fg, repo,
                    contextMenuText: "\u22ee",
                    menuHeaders: new[] {
                        "\u2702", "Delete",
                        "\u25b2", "Move Up",
                        "\u25bc", "Move Down",
                        "\u29c9", "Copy to clipboard",
                        "\u2398", "Paste from clipboard",
                    },
                    menuItemLambda: (o) =>
                    {
                        var action = false;

                        if (o is int ti)
                            switch (ti)
                            {
                                case 0:
                                    pairs.Remove(pair);
                                    action = true;
                                    break;
                                case 1:
                                    var resu = this.MoveElementInListUpwards<SpecificAssetId>(
                                        pairs, pairs[storedI]);
                                    if (resu > -1)
                                    {
                                        action = true;
                                    }
                                    break;
                                case 2:
                                    var resd = this.MoveElementInListDownwards<SpecificAssetId>(
                                        pairs, pairs[storedI]);
                                    if (resd > -1)
                                    {
                                        action = true;
                                    }
                                    break;
                                case 3:
                                    var jsonStr = JsonConvert.SerializeObject(pairs[storedI], Formatting.Indented);
                                    this.context?.ClipboardSet(new AnyUiClipboardData(jsonStr));
                                    Log.Singleton.Info("SpecificAssetId serialized to clipboard.");
                                    break;
                                case 4:
                                    try
                                    {
                                        var jsonInput = this.context?.ClipboardGet()?.Text;
                                        action = PasteIKVPTextIntoExisting(jsonInput, pairs[storedI]);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Singleton.Error(ex,
                                            "while accessing SpecificAssetId data in clipboard");
                                    }
                                    break;

                            }

                        if (action)
                        {
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }
                        return new AnyUiLambdaActionNone();
                    },
                    margin: new AnyUiThickness(2, 2, 2, 2),
                    padding: new AnyUiThickness(5, 0, 5, 0));

                AddHintBubble(
                    substack, hintMode,
                    new[] {
                        new HintCheck(
                            () => pair.SemanticId?.IsValid() != true,
                            "Check, if a semanticId can be given in addition the key!",
                            severityLevel: HintCheck.Severity.Notice)
                    });
                if (SafeguardAccess(
                        substack, repo, pair.SemanticId, "semanticId:", "Create data element!",
                        v =>
                        {
                            pair.SemanticId = new Reference(ReferenceTypes.GlobalReference, new List<Key>());
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }))
                {
                    List<string> keys = new ();
                    foreach(var semKey in pair.SemanticId.Keys)
                    {
                        keys.Add(semKey.Value);
                    }
                    AddKeyListOfIdentifier(
                        substack, "semanticId", keys, repo,
                        packages, PackageCentral.PackageCentral.Selector.MainAuxFileRepo,
                        addExistingEntities: "All",
                        addEclassIrdi: true,
                        relatedReferable: relatedReferable);
                }

                AddHintBubble(
                    substack, hintMode,
                    new[] {
                        new HintCheck(
                            () => !pair.Name.HasContent(),
                            "A key string specification shall be given!")
                    });
                AddKeyValueExRef(
                    substack, "key", pair, pair.Name, null, repo,
                    v =>
                    {
                        pair.Name = v as string;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    });

                AddKeyValueExRef(
                    substack, "value", pair, pair.Value, null, repo,
                    v =>
                    {
                        pair.Value = v as string;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    });

                if (SafeguardAccess(
                        substack, repo, pair.ExternalSubjectId, "externalSubjectId:", "Create data element!",
                        v =>
                        {
                            pair.ExternalSubjectId = new Reference(ReferenceTypes.GlobalReference, new List<Key>());
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }))
                {
                    //TODO: jtikekar: Test 
                    List<string> keys = new();
                    foreach(var extSubIdKey in pair.ExternalSubjectId.Keys)
                    {
                        keys.Add(extSubIdKey.Value);
                    }
                    AddKeyListOfIdentifier(substack, "externalSubjectId", keys, repo,
                        packages, PackageCentral.PackageCentral.Selector.MainAuxFileRepo, "All",
                        relatedReferable: relatedReferable);
                }

            }

        }

        //
        // Extensions
        //

        private bool PasteExtensionTextIntoExisting(
            string jsonInput,
            Extension qCurr)
        {
            var qIn = JsonConvert.DeserializeObject<Extension>(jsonInput);
            if (qCurr != null && qIn != null)
            {
                qCurr.Name = qIn.Name;
                qCurr.ValueType = qIn.ValueType;
                qCurr.Value = qIn.Value;
                if (qIn.RefersTo != null)
                    qCurr.RefersTo = qIn.RefersTo;
                if (qIn.SemanticId != null)
                    qCurr.SemanticId = qIn.SemanticId;
                Log.Singleton.Info("Extension data taken from clipboard.");
                return true;
            }
            return false;
        }

        // ReSharper Disable once ClassNeverInstantiated.Global
        public class ExtensionPreset
        {
            public string name = "";
            public Extension extension = new Extension("");
        }

        public void ExtensionHelper(
            AnyUiStackPanel stack, ModifyRepo repo,
            List<Extension> extensions,
            Action<List<Extension>> setOutput,
            IReferable relatedReferable = null)
        {
            // access
            if (extensions == null)
                return;

            // header
            if (editMode)
            {
                // let the user control the number of elements
                AddAction(
                    stack, "Extension entities:",
                    new[] { "Add blank", "Add preset", "Add from clipboard", "Delete last" },
                    repo,
                    (buttonNdx) =>
                    {
                        if (buttonNdx == 0)
                        {
                            extensions.Add(new Extension(""));
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        }

                        if (buttonNdx == 1)
                        {
                            var pfn = Options.Curr.ExtensionsPresetFile;
                            if (pfn == null || !System.IO.File.Exists(pfn))
                            {
                                Log.Singleton.Error(
                                    $"JSON file for IReferable.extension presets not defined nor existing ({pfn}).");
                                return new AnyUiLambdaActionNone();
                            }
                            try
                            {
                                // read file contents
                                var init = System.IO.File.ReadAllText(pfn);
                                var presets = JsonConvert.DeserializeObject<List<ExtensionPreset>>(init);

                                // define dialogue and map presets into dialogue items
                                var uc = new AnyUiDialogueDataSelectFromList();
                                uc.ListOfItems = presets.Select((pr)
                                        => new AnyUiDialogueListItem() { Text = pr.name, Tag = pr }).ToList();

                                // perform dialogue
                                this.context.StartFlyoverModal(uc);
                                if (uc.Result && uc.ResultItem?.Tag is ExtensionPreset preset
                                    && preset.extension != null)
                                {
                                    extensions.Add(preset.extension);
                                    this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Singleton.Error(
                                    ex, $"While show Qualifier presets ({pfn})");
                            }
                        }

                        if (buttonNdx == 2)
                        {
                            try
                            {
                                var eNew = new Extension("");
                                var jsonInput = this.context?.ClipboardGet()?.Text;
                                if (PasteExtensionTextIntoExisting(jsonInput, eNew))
                                {
                                    extensions.Add(eNew);
                                    this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Singleton.Error(ex, "while accessing Extension data in clipboard");
                            }
                        }

                        if (buttonNdx == 3)
                        {
                            if (extensions.Count > 0)
                                extensions.RemoveAt(extensions.Count - 1);
                            else
                                setOutput?.Invoke(null);
                        }

                        return new AnyUiLambdaActionRedrawEntity();
                    });
            }

            for (int i = 0; i < extensions.Count; i++)
            {
                var extension = extensions[i];
                var substack = AddSubStackPanel(stack, "  "); // just a bit spacing to the left

                int storedI = i;
                AddGroup(
                    substack, $"Extension {1 + i}",
                    levelColors.SubSubSection.Bg, levelColors.SubSubSection.Fg, repo,
                    contextMenuText: "\u22ee",
                    menuHeaders: new[] {
                        "\u2702", "Delete",
                        "\u25b2", "Move Up",
                        "\u25bc", "Move Down",
                        "\u29c9", "Copy to clipboard",
                        "\u2398", "Paste from clipboard",
                    },
                    menuItemLambda: (o) =>
                    {
                        var action = false;

                        if (o is int ti)
                            switch (ti)
                            {
                                case 0:
                                    extensions.Remove(extension);
                                    action = true;
                                    break;
                                case 1:
                                    var resu = this.MoveElementInListUpwards<Extension>(
                                        extensions, extensions[storedI]);
                                    if (resu > -1)
                                    {
                                        action = true;
                                    }
                                    break;
                                case 2:
                                    var resd = this.MoveElementInListDownwards<Extension>(
                                        extensions, extensions[storedI]);
                                    if (resd > -1)
                                    {
                                        action = true;
                                    }
                                    break;
                                case 3:
                                    var jsonStr = JsonConvert.SerializeObject(
                                        extensions[storedI], Formatting.Indented);
                                    this.context?.ClipboardSet(new AnyUiClipboardData(jsonStr));
                                    Log.Singleton.Info("Extension serialized to clipboard.");
                                    break;
                                case 4:
                                    try
                                    {
                                        var jsonInput = this.context?.ClipboardGet()?.Text;
                                        action = PasteExtensionTextIntoExisting(jsonInput, extensions[storedI]);
                                        if (action)
                                            Log.Singleton.Info("Extension taken from clipboard.");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Singleton.Error(ex, "while accessing Extension data in clipboard");
                                    }
                                    break;

                            }

                        if (action)
                        {
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }
                        return new AnyUiLambdaActionNone();
                    },
                    margin: new AnyUiThickness(2, 2, 2, 2),
                    padding: new AnyUiThickness(5, 0, 5, 0));

                AddHintBubble(
                    substack, hintMode,
                    new[] {
                        new HintCheck(
                            () => !extension.Name.HasContent(),
                            "A name specification shall be given and unqiue within this list!")
                    });
                AddKeyValueExRef(
                    substack, "name", extension, extension.Name, null, repo,
                    v =>
                    {
                        extension.Name = v as string;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    });
                
                AddHintBubble(
                    substack, hintMode,
                    new[] {
                        new HintCheck(
                            () => extension.SemanticId?.IsValid() != true,
                            "Check, if a semanticId can be given in addition the key!",
                            severityLevel: HintCheck.Severity.Notice)
                    });
                if (SafeguardAccess(
                        substack, repo, extension.SemanticId, "semanticId:", "Create data element!",
                        v =>
                        {
                            extension.SemanticId = new Reference(ReferenceTypes.GlobalReference, new List<Key>());
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }))
                {
                    AddVerticalSpace(substack);
                    AddKeyReference(
                        substack, "semanticId", extension.SemanticId, repo,
                        packages, PackageCentral.PackageCentral.Selector.MainAux,
                        showRefSemId: false,
                        addExistingEntities: "All", addFromKnown: true,
                        addEclassIrdi: true,
                        relatedReferable: relatedReferable,
                        auxContextHeader: new[] { "\u2573", "Delete semanticId" },
                        auxContextLambda: (i) =>
                        {
                            if (i == 0)
                            {
                                extension.SemanticId = null;
                                this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                return new AnyUiLambdaActionRedrawEntity();
                            }
                            return new AnyUiLambdaActionNone();
                        });
                    AddVerticalSpace(substack);
                }


                AddKeyValueExRef(
                    substack, "valueType", extension, Stringification.ToString(extension.ValueType), null, repo,
                    comboBoxIsEditable: editMode,
                    //comboBoxItems: DataElement.ValueTypeItems,
                    //TODO:jtikekar change
                    comboBoxItems: ExtendStringification.DataTypeXsdToStringArray().ToArray(),
                    comboBoxMinWidth: 190,
                    //new string[] {
                    //"anyURI", "base64Binary",
                    //"boolean", "date", "dateTime",
                    //"dateTimeStamp", "decimal", "integer", "long", "int", "short", "byte", "nonNegativeInteger",
                    //"positiveInteger",
                    //"unsignedLong", "unsignedInt", "unsignedShort", "unsignedByte",
                    //"nonPositiveInteger", "negativeInteger",
                    //"double", "duration",
                    //"dayTimeDuration", "yearMonthDuration", "float", "hexBinary", "string", "langString", "time" },
                    setValue: v =>
                    {
                        var vt = Stringification.DataTypeDefXsdFromString((string)v);
                        if (vt.HasValue)
                            extension.ValueType = vt.Value;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    });

                AddKeyValueExRef(
                    substack, "value", extension, extension.Value, null, repo,
                    v =>
                    {
                        extension.Value = v as string;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    },
                    limitToOneRowForNoEdit: true,
                    auxButtonTitles: new[] { "\u2261" },
                    auxButtonToolTips: new[] { "Edit in multiline editor" },
                    auxButtonLambda: (buttonNdx) =>
                    {
                        if (buttonNdx == 0)
                        {
                            var uc = new AnyUiDialogueDataTextEditor(
                                                caption: $"Edit Extension '{"" + extension.Name}'",
                                                mimeType: Stringification.ToString(extension.ValueType),
                                                text: extension.Value);
                            if (this.context.StartFlyoverModal(uc))
                            {
                                extension.Value = uc.Text;
                                this.AddDiaryEntry(relatedReferable, new DiaryEntryUpdateValue());
                                return new AnyUiLambdaActionRedrawEntity();
                            }
                        }
                        return new AnyUiLambdaActionNone();
                    });

                // refersTo are MULTIPLE ModelReference<IReferable>. That is: multiple x multiple keys!
                this.AddHintBubble(substack, hintMode, new[] {
                new HintCheck(
                    () => extension.RefersTo?.IsValid() == true,
                    "Currently, AAS croe allows only for 0..1 refersTo references.",
                    breakIfTrue: true,
                    severityLevel: HintCheck.Severity.Notice) });
                if (this.SafeguardAccess(
                        substack, this.repo, extension.RefersTo, "refersTo:", "Create data element!",
                        v =>
                        {
                            extension.RefersTo = new Reference(ReferenceTypes.ModelReference, new List<Key>());
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }))
                {
                    if (editMode)
                    {
                        // let the user control the number of references
                        this.AddAction(
                            substack, "refersTo:",
                            new[] { "Add Reference", "Delete last reference" }, repo,
                            (buttonNdx) =>
                            {
                                if (buttonNdx == 0 && extension.RefersTo?.IsValid() != true)
                                    extension.RefersTo = new Reference(ReferenceTypes.ModelReference, new List<Key>());

                                if (buttonNdx == 1 && extension.RefersTo != null)
                                    extension.RefersTo = null;

                                this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                                return new AnyUiLambdaActionRedrawEntity();
                            });
                    }

                    // now use the normal mechanism to deal with editMode or not..
                    if (extension.RefersTo != null && extension.RefersTo != null)
                    {
                        //for (int ki = 0; ki < extension.RefersTo.Count; ki++)
                        //    if (extension.RefersTo[ki] != null)
                        this.AddKeyReference(
                            substack, String.Format("refersTo[{0}]", 0),
                            extension.RefersTo,
                            repo, packages, PackageCentral.PackageCentral.Selector.MainAux,
                            addExistingEntities: "All", addFromKnown: true,
                            addEclassIrdi: true,
                            showRefSemId: false);
                    }
                }

            }

        }

        //
        // References
        //

        public void AddKeyReference(
        AnyUiStackPanel view, string key,
        Reference refkeys,
        ModifyRepo repo = null,
        PackageCentral.PackageCentral packages = null,
        PackageCentral.PackageCentral.Selector selector = PackageCentral.PackageCentral.Selector.Main,
        string addExistingEntities = null,
        bool addEclassIrdi = false,
        bool addFromKnown = false,
        string[] addPresetNames = null, List<Key>[] addPresetKeyLists = null,
        Func<List<Key>, AnyUiLambdaActionBase> jumpLambda = null,
        AnyUiLambdaActionBase takeOverLambdaAction = null,
        Func<List<Key>, AnyUiLambdaActionBase> noEditJumpLambda = null,
        IReferable relatedReferable = null,
        Action<IReferable> emitCustomEvent = null,
        bool showRefSemId = true,
        string[] auxContextHeader = null, Func<int, AnyUiLambdaActionBase> auxContextLambda = null)
        {
            //
            // extended Front panel
            //

            var frontPanel = AddSmallGrid(1, 3, new[] { "#", "#", "*" });

            AnyUiUIElement.RegisterControl(
                AddSmallComboBoxTo(
                    frontPanel, 0, 0,
                    margin: new AnyUiThickness(4, 2, 2, 2),
                    padding: new AnyUiThickness(2, -1, 0, -1),
                    text: "" + Stringification.ToString(refkeys.Type),
                    minWidth: 100,
                    items: Enum.GetValues(typeof(ReferenceTypes)).OfType<ReferenceTypes>().Select((rt) => Stringification.ToString(rt)).ToArray(),
                    isEditable: false,
                    verticalContentAlignment: AnyUiVerticalAlignment.Center),
                (o) =>
                {
                    if (o is string os)
                        refkeys.Type = Stringification.ReferenceTypesFromString(os).Value;
                    return new AnyUiLambdaActionNone();
                },
                takeOverLambda: takeOverLambdaAction);

            AnyUiUIElement.RegisterControl(
                        AddSmallButtonTo(
                            frontPanel, 0, 1,
                            margin: new AnyUiThickness(2, 2, 2, 2),
                            padding: new AnyUiThickness(5, 0, 5, 0),
                            content: "\u21bb"),
                        (o) =>
                        {
                            refkeys.Type = refkeys.GuessType();
                            return new AnyUiLambdaActionRedrawEntity();
                        }
                        );

            //
            // referredSemanticId?
            //

            AnyUiStackPanel footerPanel = null;
            if (showRefSemId)
            {
                footerPanel = new AnyUiStackPanel() { 
                    Margin = new AnyUiThickness(2, 2, 0, 0),
                    Background = AnyUiBrushes.LightGray
                };
                
                if (refkeys.ReferredSemanticId == null)
                {
                    if (repo != null)
                    {
                        var gx = AddSmallGrid(1, 3, new[] { "*", "#", "*" }, margin: new AnyUiThickness(2, 2, 0, 0));
                        footerPanel.Add(gx);

                        AddSmallBasicLabelTo(gx, 0, 0, content: "referredSemanticId:", verticalAlignment: AnyUiVerticalAlignment.Center);

                        AnyUiUIElement.RegisterControl(
                            AddSmallButtonTo(gx, 0, 1, content: "+",  // content: "\u25be",
                            margin: new AnyUiThickness(1),
                            padding: new AnyUiThickness(2, -2, 2, -2)),
                            (o) =>
                            {
                                refkeys.ReferredSemanticId = new Reference(ReferenceTypes.GlobalReference, new List<Key>());
                                return new AnyUiLambdaActionRedrawEntity();
                            });
                    }
                }
                else
                {
                    // careful! Full recursion of edit function
                    AddKeyReference(
                        footerPanel, "referredSem.Id", refkeys.ReferredSemanticId, repo,
                        showRefSemId: false,
                        addEclassIrdi: true,
                        addFromKnown: true,
                        auxContextHeader: new[] { "\u2573", "Delete referredSemanticId" },
                        auxContextLambda: (i) =>
                        {
                            if (i == 0)
                            {
                                refkeys.ReferredSemanticId = null;
                                return new AnyUiLambdaActionRedrawEntity();
                            }
                            return new AnyUiLambdaActionNone();
                        });
                }
            }

            //
            // refer further
            //

            AddKeyListKeys(
                view, key, refkeys.Keys, repo, packages, selector, 
                addExistingEntities, addEclassIrdi, addFromKnown, addPresetNames, addPresetKeyLists, 
                jumpLambda, takeOverLambdaAction, noEditJumpLambda, 
                relatedReferable, 
                frontPanel: frontPanel,
                topContextMenu: true,
                footerPanel: footerPanel,
                auxContextHeader: auxContextHeader,
                auxContextLambda: auxContextLambda,
                emitCustomEvent: (o) =>
                {
                    // use the custom event as event for fired when changed keys
                    refkeys.Type = refkeys.GuessType();

                    // pass on
                    emitCustomEvent?.Invoke(o);
                });
        }

        //
        // ValueList of CD
        //

        public void ValueListHelper(
            AnyUiStackPanel stack, ModifyRepo repo, string key,
            List<ValueReferencePair> valuePairs,
            IReferable relatedReferable = null)
        {
            if (editMode)
            {
                // let the user control the number of pairs
                AddAction(
                    stack, $"{key}:",
                    new[] { "Add blank", "Delete last" },
                    repo,
                    (buttonNdx) =>
                    {
                        if (buttonNdx == 0)
                        {
                            valuePairs.Add(new ValueReferencePair("", null));
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        }

                        if (buttonNdx == 1 && valuePairs.Count > 0)
                            valuePairs.RemoveAt(valuePairs.Count - 1);

                        return new AnyUiLambdaActionRedrawEntity();
                    });
            }

            for (int i = 0; i < valuePairs.Count; i++)
            {
                var vp = valuePairs[i];
                var substack = AddSubStackPanel(stack, "", minWidthFirstCol: this.smallFirstColWidth); 

                int storedI = i;
                AddGroup(
                    substack, $"Pair {1 + i}",
                    levelColors.SubSubSection.Bg, levelColors.SubSubSection.Fg, repo,
                    contextMenuText: "\u22ee",
                    menuHeaders: new[] {
                        "\u2702", "Delete",
                        "\u25b2", "Move Up",
                        "\u25bc", "Move Down"
                    },
                    menuItemLambda: (o) =>
                    {
                        var action = false;

                        if (o is int ti)
                            switch (ti)
                            {
                                case 0:
                                    valuePairs.Remove(vp);
                                    action = true;
                                    break;
                                case 1:
                                    var resu = this.MoveElementInListUpwards<ValueReferencePair>(
                                        valuePairs, valuePairs[storedI]);
                                    if (resu > -1)
                                    {
                                        action = true;
                                    }
                                    break;
                                case 2:
                                    var resd = this.MoveElementInListDownwards<ValueReferencePair>(
                                        valuePairs, valuePairs[storedI]);
                                    if (resd > -1)
                                    {
                                        action = true;
                                    }
                                    break;

                            }

                        if (action)
                        {
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }
                        return new AnyUiLambdaActionNone();
                    },
                    margin: new AnyUiThickness(2, 2, 2, 2),
                    padding: new AnyUiThickness(5, 0, 5, 0));

                AddKeyValueExRef(
                    substack, "value", vp, vp.Value, null, repo,
                    v =>
                    {
                        vp.Value = v as string;
                        this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                        return new AnyUiLambdaActionNone();
                    });

                if (SafeguardAccess(
                        substack, repo, vp.ValueId, "valueId:", "Create data element!",
                        v =>
                        {
                            vp.ValueId = new Reference(ReferenceTypes.GlobalReference, new List<Key>());
                            this.AddDiaryEntry(relatedReferable, new DiaryEntryStructChange());
                            return new AnyUiLambdaActionRedrawEntity();
                        }))
                {
                    AddKeyReference(substack, "valueId", vp.ValueId, repo,
                        packages, PackageCentral.PackageCentral.Selector.MainAuxFileRepo, 
                        addExistingEntities: Stringification.ToString(KeyTypes.ConceptDescription),
                        addFromKnown: true, showRefSemId: false,
                        relatedReferable: relatedReferable);
                }

            }

        }

    }
}