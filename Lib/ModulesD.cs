﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Souvenir;
using UnityEngine;
using Rnd = UnityEngine.Random;

public partial class SouvenirModule
{
    private IEnumerator<YieldInstruction> ProcessDACHMaze(ModuleData module)
    {
        return processWorldMaze(module, "DACHMaze", Question.DACHMazeOrigin);
    }

    private IEnumerator<YieldInstruction> ProcessDeafAlley(ModuleData module)
    {
        var comp = GetComponent(module, "DeafAlleyScript");
        var shapes = GetField<string[]>(comp, "shapes").Get();

        yield return WaitForSolve;

        var selectedShape = GetField<int>(comp, "selectedShape").Get();
        addQuestion(module, Question.DeafAlleyShape, correctAnswers: new[] { shapes[selectedShape] }, preferredWrongAnswers: shapes);
    }

    private IEnumerator<YieldInstruction> ProcessDeckOfManyThings(ModuleData module)
    {
        var comp = GetComponent(module, "deckOfManyThingsScript");
        var fldSolution = GetIntField(comp, "solution");

        yield return WaitForSolve;

        var deck = GetField<Array>(comp, "deck").Get(d => d.Length == 0 ? "deck is empty" : null);
        var btns = GetArrayField<KMSelectable>(comp, "btns", isPublic: true).Get(expectedLength: 2);
        var prevCard = GetField<KMSelectable>(comp, "prevCard", isPublic: true).Get();
        var nextCard = GetField<KMSelectable>(comp, "nextCard", isPublic: true).Get();

        prevCard.OnInteract = delegate { return false; };
        nextCard.OnInteract = delegate { return false; };
        foreach (var btn in btns)
            btn.OnInteract = delegate
            {
                Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, btn.transform);
                btn.AddInteractionPunch(0.5f);
                return false;
            };

        string firstCardDeck = deck.GetValue(0).GetType().ToString().Replace("Card", "");

        // correcting original misspelling
        if (firstCardDeck == "Artic")
            firstCardDeck = "Arctic";

        var solution = fldSolution.Get();

        if (solution == 0)
        {
            Debug.Log($"[Souvenir #{_moduleId}] No question for The Deck of Many Things because the solution was the first card.");
            _legitimatelyNoQuestions.Add(module.Module);
            yield break;
        }

        addQuestion(module, Question.DeckOfManyThingsFirstCard, correctAnswers: new[] { firstCardDeck });
    }

    private IEnumerator<YieldInstruction> ProcessDecoloredSquares(ModuleData module)
    {
        var comp = GetComponent(module, "DecoloredSquaresModule");
        yield return WaitForSolve;

        var colColor = GetField<string>(comp, "_color1").Get();
        var rowColor = GetField<string>(comp, "_color2").Get();

        addQuestions(module,
            makeQuestion(Question.DecoloredSquaresStartingPos, module, formatArgs: new[] { "column" }, correctAnswers: new[] { colColor }),
            makeQuestion(Question.DecoloredSquaresStartingPos, module, formatArgs: new[] { "row" }, correctAnswers: new[] { rowColor }));
    }

    private IEnumerator<YieldInstruction> ProcessDecolourFlash(ModuleData module)
    {
        var comp = GetComponent(module, "DecolourFlashScript");
        yield return WaitForSolve;

        var names = new[] { "Blue", "Green", "Red", "Magenta", "Yellow", "White" };
        var goals = GetField<IList>(comp, "_goals").Get(validator: l => l.Count != 3 ? "expected length 3" : null);
        var hexGrid = GetField<IDictionary>(comp, "_hexes").Get(validator: d => !goals.Cast<object>().All(g => d.Contains(g)) ? "key missing in dictionary" : null);
        var infos = goals.Cast<object>().Select(goal => hexGrid[goal]).ToArray();
        var fldColour = GetField<object>(infos[0], "ColourIx");
        var fldWord = GetField<object>(infos[0], "Word");
        var colours = infos.Select(inf => (int) fldColour.GetFrom(inf)).ToArray();
        var words = infos.Select(inf => (int) fldWord.GetFrom(inf)).ToArray();
        if (colours.Any(c => c < 0 || c >= 6) || words.Any(w => w < 0 || w >= 6))
            throw new AbandonModuleException($"colours/words are: [{colours.JoinString(", ")}], [{words.JoinString(", ")}]; expected values 0–5");

        var qs = new List<QandA>();
        for (var i = 0; i < 3; i++)
        {
            qs.Add(makeQuestion(Question.DecolourFlashGoal, module, formatArgs: new[] { "colour", Ordinal(i + 1) }, correctAnswers: new[] { names[colours[i]] }));
            qs.Add(makeQuestion(Question.DecolourFlashGoal, module, formatArgs: new[] { "word", Ordinal(i + 1) }, correctAnswers: new[] { names[words[i]] }));
        }
        addQuestions(module, qs);
    }

    private IEnumerator<YieldInstruction> ProcessDenialDisplays(ModuleData module)
    {
        var comp = GetComponent(module, "DenialDisplaysScript");
        yield return WaitForSolve;

        var initial = GetArrayField<int>(comp, "_initialDisplayNums").Get();

        var rands = new List<int>();
        for (int i = 0; i < 50; i++)
        {
            int r = Rnd.Range(0, 3);
            if (r == 0)
                rands.Add(Rnd.Range(0, 10));
            else if (r == 1)
                rands.Add(Rnd.Range(10, 100));
            else
                rands.Add(Rnd.Range(100, 1000));
        }

        var qs = new List<QandA>();
        for (int disp = 0; disp < 5; disp++)
            qs.Add(makeQuestion(Question.DenialDisplaysDisplays, module,
                formatArgs: new[] { "ABCDE"[disp].ToString() },
                correctAnswers: new[] { initial[disp].ToString() },
                preferredWrongAnswers: rands.Select(i => i.ToString()).ToArray()));

        addQuestions(module, qs);
    }

    private IEnumerator<YieldInstruction> ProcessDetoNATO(ModuleData module)
    {
        var comp = GetComponent(module, "Detonato");
        var fldStage = GetIntField(comp, "stage");
        var words = GetArrayField<string>(comp, "words").Get(expectedLength: 156);
        var textMesh = GetField<TextMesh>(comp, "screenText", true).Get();
        var displaysList = new List<string>();
        var currentStage = -1;
        while (module.Unsolved)
        {
            var newStage = fldStage.Get();
            var currentWord = textMesh.text;
            if (currentWord != "")
            {
                if (newStage != currentStage || currentStage >= displaysList.Count)
                {
                    displaysList.Add(currentWord);
                    currentStage = newStage;
                }
                else
                    displaysList[currentStage] = currentWord;
            }
            yield return null;
        }
        yield return WaitForSolve;
        addQuestions(module, displaysList.Select((w, ix) => makeQuestion(Question.DetoNATODisplay, module,
            formatArgs: new[] { Ordinal(ix + 1) },
            correctAnswers: new[] { displaysList[ix] },
            allAnswers: words)));
    }

    private IEnumerator<YieldInstruction> ProcessDevilishEggs(ModuleData module)
    {
        var comp = GetComponent(module, "devilishEggs");
        var prismTexts = GetArrayField<TextMesh>(comp, "prismTexts", isPublic: true).Get(expectedLength: 3);
        var digits = prismTexts[0].text.Split(' ');
        var letters = prismTexts[1].text.Split(' ');
        if (digits.Length != 8 || digits.Any(str => str.Length != 1 || str[0] < '0' || str[0] > '9'))
            throw new AbandonModuleException($"Expected 8 digits; got {digits.Stringify()}");
        if (letters.Length != 8 || letters.Any(str => str.Length != 1 || str[0] < 'A' || str[0] > 'Z'))
            throw new AbandonModuleException($"Expected 8 letters; got {letters.Stringify()}");

        yield return WaitForSolve;

        var topRotations = GetField<Array>(comp, "topRotations").Get(validator: arr => arr.Length != 6 ? "expected length 6" : null).Cast<object>().Select(rot => rot.ToString()).ToArray();
        var bottomRotations = GetField<Array>(comp, "bottomRotations").Get(validator: arr => arr.Length != 6 ? "expected length 6" : null).Cast<object>().Select(rot => rot.ToString()).ToArray();
        var allRotations = topRotations.Concat(bottomRotations).ToArray();

        var qs = new List<QandA>();
        for (var rotIx = 0; rotIx < 6; rotIx++)
        {
            qs.Add(makeQuestion(Question.DevilishEggsRotations, module, formatArgs: new[] { "top", Ordinal(rotIx + 1) }, correctAnswers: new[] { topRotations[rotIx] }, preferredWrongAnswers: allRotations));
            qs.Add(makeQuestion(Question.DevilishEggsRotations, module, formatArgs: new[] { "bottom", Ordinal(rotIx + 1) }, correctAnswers: new[] { bottomRotations[rotIx] }, preferredWrongAnswers: allRotations));
        }
        for (var ix = 0; ix < 8; ix++)
        {
            qs.Add(makeQuestion(Question.DevilishEggsNumbers, module, formatArgs: new[] { Ordinal(ix + 1) }, correctAnswers: new[] { digits[ix] }, preferredWrongAnswers: digits));
            qs.Add(makeQuestion(Question.DevilishEggsLetters, module, formatArgs: new[] { Ordinal(ix + 1) }, correctAnswers: new[] { letters[ix] }, preferredWrongAnswers: letters));
        }
        addQuestions(module, qs);
    }

    private IEnumerator<YieldInstruction> ProcessDigisibility(ModuleData module)
    {
        var comp = GetComponent(module, "digisibilityScript");
        yield return WaitForSolve;

        var displayedNums = GetField<int[][]>(comp, "Data").Get().First();

        var qs = new List<QandA>();
        for (int i = 0; i < 9; i++)
            qs.Add(makeQuestion(Question.DigisibilityDisplayedNumber, module,
                formatArgs: new[] { Ordinal(i + 1) },
                correctAnswers: new[] { displayedNums[i].ToString() },
                preferredWrongAnswers: displayedNums.Select(x => x.ToString()).ToArray()));
        addQuestions(module, qs);
    }

    private IEnumerator<YieldInstruction> ProcessDigitString(ModuleData module)
    {
        var comp = GetComponent(module, "digitString");
        yield return WaitForSolve;

        var storedInitialString = GetField<string>(comp, "shownString").Get(x => x.Length != 8 ? "Expected length 8" : null);

        addQuestion(module, Question.DigitStringInitialNumber, correctAnswers: new[] { storedInitialString });
    }

    private IEnumerator<YieldInstruction> ProcessDimensionDisruption(ModuleData module)
    {
        var comp = GetComponent(module, "dimensionDisruptionScript");

        var letterIndex = new List<int>()
        {
            GetField<int>(comp, "letOne").Get(),
            GetField<int>(comp, "letTwo").Get(),
            GetField<int>(comp, "letThree").Get()
        };

        yield return WaitForSolve;

        var alphabet = GetField<string>(comp, "alphabet").Get();
        var answers = letterIndex.Select(li => alphabet[li].ToString()).ToArray();
        addQuestion(module, Question.DimensionDisruptionVisibleLetters, correctAnswers: answers);
    }

    private IEnumerator<YieldInstruction> ProcessDiscoloredSquares(ModuleData module)
    {
        var comp = GetComponent(module, "DiscoloredSquaresModule");
        yield return WaitForSolve;

        var colorsRaw = GetField<Array>(comp, "_rememberedColors").Get(arr => arr.Length != 4 ? "expected length 4" : null);
        var positions = GetArrayField<int>(comp, "_rememberedPositions").Get(expectedLength: 4);
        var colors = colorsRaw.Cast<object>().Select(obj => obj.ToString()).ToArray();

        addQuestions(module, Enumerable.Range(0, 4).Select(color =>
            makeQuestion(Question.DiscoloredSquaresRememberedPositions, module, formatArgs: new[] { colors[color] }, correctAnswers: new[] { new Coord(4, 4, positions[color]) })));
    }

    private IEnumerator<YieldInstruction> ProcessDirectionalButton(ModuleData module)
    {
        var comp = GetComponent(module, "DirectrionalButtonScripty");
        var fldStage = GetIntField(comp, "Stage");
        var fldCorrectPres = GetIntField(comp, "CorrectPres");

        var currentStage = 0;
        var currentCorrectPress = 0;
        var buttonPresses = new int[5];

        while (module.Unsolved)
        {
            var stage = fldStage.Get();
            var correctPress = fldCorrectPres.Get();

            if (stage != currentStage || correctPress != currentCorrectPress)
            {
                currentStage = stage;
                currentCorrectPress = correctPress;
                buttonPresses[currentStage - 1] = currentCorrectPress;
            }

            yield return null;
        }

        addQuestions(module, Enumerable.Range(0, 5).Select(i =>
            makeQuestion(Question.DirectionalButtonButtonCount, module, formatArgs: new[] { Ordinal(i + 1) }, correctAnswers: new[] { buttonPresses[i].ToString() })));
    }

    private IEnumerator<YieldInstruction> ProcessDisorderedKeys(ModuleData module)
    {
        var comp = GetComponent(module, "DisorderedKeysScript");
        var fldMissing = GetArrayField<int>(comp, "missing");
        var fldInfo = GetArrayField<int[]>(comp, "info");
        var fldQuirk = GetArrayField<int>(comp, "quirk");
        var colorList = GetStaticField<string[]>(comp.GetType(), "colourList").Get(v => v.Length != 6 ? "expected length 6" : null);

        // These variables are populated by GetInfo() below
        int[] missing = null;
        int[][] info = null;
        int[] quirks = null;
        var unrevealedKeyColors = new string[6];
        var unrevealedLabels = new string[6];
        var unrevealedLabelColors = new string[6];
        var recompute = true;

        module.Module.OnStrike += () =>
        {
            recompute = true;
            return false;
        };

        while (module.Unsolved)
        {
            yield return null;
            if (recompute)
            {
                missing = fldMissing.Get(expectedLength: 6, validator: number => number < 0 || number > 2 ? "expected range 0–2 inclusively" : null).ToArray();
                info = fldInfo.Get(expectedLength: 6, validator: arr => arr.Length != 3 ? "expected length 3" : null).ToArray();
                quirks = fldQuirk.Get(expectedLength: 6).ToArray();

                for (var keyIndex = 0; keyIndex < 6; keyIndex++)
                {
                    unrevealedKeyColors[keyIndex] = missing[keyIndex] == 0 ? "missing" : colorList[info[keyIndex][0]];
                    unrevealedLabelColors[keyIndex] = missing[keyIndex] == 1 ? "missing" : colorList[info[keyIndex][1]];
                    unrevealedLabels[keyIndex] = missing[keyIndex] == 2 ? "missing" : (info[keyIndex][2] + 1).ToString();
                }

                recompute = false;
            }
        }

        var qs = new List<QandA>();
        var missingStrArr = new[] { "Key color", "Label color", "Label" };

        for (var keyIndex = 0; keyIndex < 6; keyIndex++)
        {
            var formatArgs = new[] { Ordinal(keyIndex + 1) };
            qs.Add(makeQuestion(Question.DisorderedKeysMissingInfo, module, formatArgs: formatArgs, correctAnswers: new[] { missingStrArr[missing[keyIndex]] }));

            if (missing[keyIndex] != 0)   // Key color
                qs.Add(makeQuestion(Question.DisorderedKeysUnrevealedKeyColor, module, formatArgs: formatArgs, correctAnswers: new[] { unrevealedKeyColors[keyIndex] }));
            if (missing[keyIndex] != 1)     // Label color
                qs.Add(makeQuestion(Question.DisorderedKeysUnrevealedLabelColor, module, formatArgs: formatArgs, correctAnswers: new[] { unrevealedLabelColors[keyIndex] }));
            if (missing[keyIndex] != 2)     // Label
                qs.Add(makeQuestion(Question.DisorderedKeysUnrevealedKeyLabel, module, formatArgs: formatArgs, correctAnswers: new[] { unrevealedLabels[keyIndex] }));

            // If not a sequential nor false key, ask about reavealed key info
            if (quirks[keyIndex] < 4)
            {
                qs.Add(makeQuestion(Question.DisorderedKeysRevealedKeyColor, module, formatArgs: formatArgs, correctAnswers: new[] { colorList[info[keyIndex][0]] }));
                qs.Add(makeQuestion(Question.DisorderedKeysRevealedLabelColor, module, formatArgs: formatArgs, correctAnswers: new[] { colorList[info[keyIndex][1]] }));
                qs.Add(makeQuestion(Question.DisorderedKeysRevealedLabel, module, formatArgs: formatArgs, correctAnswers: new[] { (info[keyIndex][2] + 1).ToString() }));
            }
        }
        addQuestions(module, qs);
    }

    private IEnumerator<YieldInstruction> ProcessDivisibleNumbers(ModuleData module)
    {
        var comp = GetComponent(module, "DivisableNumbers");
        yield return WaitForSolve;

        var finalNumbers = GetArrayField<int>(comp, "finalNumbers").Get(expectedLength: 3, validator: number => number < 0 || number > 9999 ? "expected range 0–9999" : null);
        var finalNumbersStr = finalNumbers.Select(n => n.ToString()).ToArray();

        var qs = new List<QandA>();
        for (int i = 0; i < finalNumbers.Length; i++)
            qs.Add(makeQuestion(Question.DivisibleNumbersNumbers, module, formatArgs: new[] { Ordinal(i + 1) }, correctAnswers: new[] { finalNumbersStr[i] }, preferredWrongAnswers: finalNumbersStr));
        addQuestions(module, qs);
    }

    private IEnumerator<YieldInstruction> ProcessDoubleArrows(ModuleData module)
    {
        var comp = GetComponent(module, "DoubleArrowsScript");
        var fldPresses = GetField<int>(comp, "pressCount");
        var display = GetField<TextMesh>(comp, "disp", true).Get();
        var start = "";

        while (module.Unsolved)
        {
            if (display.text.Length == 2)
                start = display.text; // This resets on a strike.
            yield return new WaitForSeconds(.1f);
        }

        var qs = new List<QandA>(17) { makeQuestion(Question.DoubleArrowsStart, module, correctAnswers: new[] { start }) };
        var callib = GetArrayField<int[]>(comp, "callib").Get(expectedLength: 2);
        var dirs = new[] { "Left", "Up", "Right", "Down" };
        for (int i = 0; i < 8; i++)
        {
            qs.Add(makeQuestion(Question.DoubleArrowsMovement, module, formatArgs: new[] { $"{(i < 4 ? "inner" : "outer")} {dirs[i % 4].ToLowerInvariant()}" }, correctAnswers: new[] { dirs[callib[i / 4][i % 4]] }));
            qs.Add(makeQuestion(Question.DoubleArrowsArrow, module, formatArgs: new[] { i < 4 ? "inner" : "outer", dirs[callib[i / 4][i % 4]].ToLowerInvariant() }, correctAnswers: new[] { dirs[i % 4] }));
        }

        addQuestions(module, qs);
    }

    private IEnumerator<YieldInstruction> ProcessDoubleColor(ModuleData module)
    {
        var comp = GetComponent(module, "doubleColor");
        var fldColor = GetIntField(comp, "screenColor");
        var fldStage = GetIntField(comp, "stageNumber");

        while (!_isActivated)
            yield return new WaitForSeconds(.1f);

        var color1 = fldColor.Get(min: 0, max: 4);
        var stage = fldStage.Get(min: 1, max: 1);
        var submitBtn = GetField<KMSelectable>(comp, "submit", isPublic: true).Get();

        var prevInteract = submitBtn.OnInteract;
        submitBtn.OnInteract = delegate
        {
            var ret = prevInteract();
            stage = fldStage.Get();
            if (stage == 1)  // This means the user got a strike. Need to retrieve the new first stage color
                // We mustn’t throw an exception inside of the button handler, so don’t check min/max values here
                color1 = fldColor.Get();
            return ret;
        };

        yield return WaitForSolve;

        // Check the value of color1 because we might have reassigned it inside the button handler
        if (color1 < 0 || color1 > 4)
            throw new AbandonModuleException($"First stage color has unexpected value: {color1} (expected 0 to 4).");

        var color2 = fldColor.Get(min: 0, max: 4);

        var colorNames = new[] { "Green", "Blue", "Red", "Pink", "Yellow" };

        addQuestions(module,
            makeQuestion(Question.DoubleColorColors, module, formatArgs: new[] { "first" }, correctAnswers: new[] { colorNames[color1] }),
            makeQuestion(Question.DoubleColorColors, module, formatArgs: new[] { "second" }, correctAnswers: new[] { colorNames[color2] }));
    }

    private IEnumerator<YieldInstruction> ProcessDoubleDigits(ModuleData module)
    {
        var comp = GetComponent(module, "DoubleDigitsScript");
        yield return WaitForSolve;

        var d = GetArrayField<int>(comp, "digits").Get();
        var digits = Enumerable.Range(0, d.Length).Select(str => d[str].ToString()).ToArray();

        addQuestions(module,
            makeQuestion(Question.DoubleDigitsDisplays, module, formatArgs: new[] { "left" }, correctAnswers: new[] { digits[0] }),
            makeQuestion(Question.DoubleDigitsDisplays, module, formatArgs: new[] { "right" }, correctAnswers: new[] { digits[1] }));
    }

    private IEnumerator<YieldInstruction> ProcessDoubleExpert(ModuleData module)
    {
        var comp = GetComponent(module, "doubleExpertScript");

        yield return WaitForSolve;

        var startingKeyNumber = GetIntField(comp, "startKeyNumber").Get(min: 30, max: 69);
        var keywords = GetListField<string>(comp, "keywords").Get().ToArray();
        var correctKeywordIndex = GetIntField(comp, "correctKeyword").Get(min: 0, max: keywords.Length - 1);

        addQuestions(
            module,
            makeQuestion(Question.DoubleExpertStartingKeyNumber, module, correctAnswers: new[] { startingKeyNumber.ToString() }),
            makeQuestion(Question.DoubleExpertSubmittedWord, module, correctAnswers: new[] { keywords[correctKeywordIndex] }, preferredWrongAnswers: keywords)
        );
    }

    private IEnumerator<YieldInstruction> ProcessDoubleListening(ModuleData module)
    {
        var comp = GetComponent(module, "doubleListeningScript");
        yield return WaitForSolve;

        // Sounds could be gotten directly from the module, however,
        // they can't be for Listening so there's no point.

        var indices = new[] {
            0,  2,  3,  4,  5,  //"Arcade","Beach","Book Page Turning","Car Engine","Casino",
            6,  7,  8,  9,  10, //"Censorship Bleep","Chainsaw","Compressed Air","Cow","Dialup Internet",
            11, 12, 13, 14, 15, //"Door Closing","Extractor Fan","Firework Exploding","Glass Shattering","Helicopter",
            16, 17, 18, 19, 20, //"Marimba","Medieval Weapons","Oboe","Phone Ringing","Police Radio Scanner",
            21, 22, 23, 24, 25, //"Rattling Iron Chain","Reloading Glock 19","Saxophone","Servo Motor","Sewing Machine",
            26, 27, 28, 29, 30, //"Soccer Match","Squeaky Toy","Supermarket","Table Tennis","Tawny Owl",
            31, 33, 34, 35, 36, //"Taxi Dispatch","Throat Singing","Thrush Nightingale","Tibetan Nuns","Train Station",
            37, 38, 39          //"Tuba","Vacuum Cleaner","Waterfall"
        };

        var used = GetArrayField<int>(comp, "soundPositions").Get(expectedLength: 2, validator: i => i < 0 || i >= indices.Length ? $"Index {i} out of range [0,{indices.Length})" : null);
        addQuestion(module, Question.DoubleListeningSounds,
            correctAnswers: used.Select(i => ListeningAudio[indices[i]]).ToArray(),
            allAnswers: indices.Select(i => ListeningAudio[i]).ToArray()
        );
    }

    private IEnumerator<YieldInstruction> ProcessDoubleOh(ModuleData module)
    {
        var comp = GetComponent(module, "DoubleOhModule");
        yield return WaitForSolve;

        var submitIndex = GetField<Array>(comp, "_functions").Get().Cast<object>().IndexOf(f => f.ToString() == "Submit");
        if (submitIndex < 0 || submitIndex > 4)
            throw new AbandonModuleException($"Submit button is at index {submitIndex} (expected 0–4).");

        addQuestion(module, Question.DoubleOhSubmitButton, correctAnswers: new[] { "↕↔⇔⇕◆".Substring(submitIndex, 1) });
    }

    private IEnumerator<YieldInstruction> ProcessDoubleScreen(ModuleData module)
    {
        var comp = GetComponent(module, "DoubleScreenScript");

        List<(int Top, int Bottom)> stages = new();
        module.Module.OnStrike += () => { stages.Clear(); return false; };

        yield return null;  // Ensures that the module’s Start() method has run
        var stageCount = GetField<int>(comp, "stageCount").Get(v => v is < 2 or > 3 ? $"Bad stage count {v}" : null);
        var screen = GetArrayField<GameObject>(comp, "screens", isPublic: true).Get(expectedLength: 2)[0];
        var colors = GetArrayField<int>(comp, "colors");

        bool newStage = true;
        while (module.Unsolved)
        {
            if (newStage && screen.activeSelf)
            {
                newStage = false;
                var col = colors.Get(expectedLength: 2, validator: i => i is < 0 or > 3 ? $"Bad color {i}" : null);
                stages.Add((col[0], col[1]));
            }
            else if (!newStage && !screen.activeSelf)
                newStage = true;

            // Screens are off for 0.2s between stages and only turn back on after stage generation.
            yield return new WaitForSeconds(.1f);
        }

        if (stages.Count != stageCount)
            throw new AbandonModuleException($"Expected {stageCount} stages but found {stages.Count}.");

        var colorNames = new string[] { "Red", "Yellow", "Green", "Blue" };
        addQuestions(module, stages.SelectMany((s, i) => new QandA[] {
                makeQuestion(Question.DoubleScreenColors, module, correctAnswers: new[] { colorNames[s.Top] }, formatArgs: new[] { "top", Ordinal(i + 1) }),
                makeQuestion(Question.DoubleScreenColors, module, correctAnswers: new[] { colorNames[s.Bottom] }, formatArgs: new[] { "bottom", Ordinal(i + 1) })
        }));
    }

    private IEnumerator<YieldInstruction> ProcessDrDoctor(ModuleData module)
    {
        var comp = GetComponent(module, "DrDoctorModule");
        yield return WaitForSolve;

        var diagnoses = GetArrayField<string>(comp, "_selectableDiagnoses").Get();
        var symptoms = GetArrayField<string>(comp, "_selectableSymptoms").Get();
        var diagnoseText = GetField<TextMesh>(comp, "DiagnoseText", isPublic: true).Get();

        addQuestions(module,
            makeQuestion(Question.DrDoctorDiseases, module, correctAnswers: diagnoses.Except(new[] { diagnoseText.text }).ToArray()),
            makeQuestion(Question.DrDoctorSymptoms, module, correctAnswers: symptoms));
    }

    private IEnumerator<YieldInstruction> ProcessDreamcipher(ModuleData module)
    {
        var comp = GetComponent(module, "Dreamcipher");
        var wordList = JsonConvert.DeserializeObject<string[]>(GetField<TextAsset>(comp, "wordList", isPublic: true).Get().text);

        yield return WaitForSolve;

        string targetWord = GetField<string>(comp, "targetWord").Get().ToLowerInvariant();
        addQuestion(module, Question.DreamcipherWord, correctAnswers: new[] { targetWord }, preferredWrongAnswers: wordList);
    }

    private IEnumerator<YieldInstruction> ProcessDuck(ModuleData module)
    {
        var comp = GetComponent(module, "theDuckScript");

        yield return WaitForSolve;

        var colorNames = new[] { "blue", "yellow", "green", "orange", "red" };
        var curtainColor = colorNames[GetIntField(comp, "curtainColor").Get(min: 0, max: 4)];

        addQuestions(
            module,
            makeQuestion(Question.DuckCurtainColor, module, correctAnswers: new[] { curtainColor })
        );
    }

    private IEnumerator<YieldInstruction> ProcessDumbWaiters(ModuleData module)
    {
        var comp = GetComponent(module, "dumbWaiters");
        yield return WaitForSolve;

        var players = GetStaticField<string[]>(comp.GetType(), "names").Get();
        var playersAvaiable = GetArrayField<int>(comp, "presentPlayers").Get();
        var availablePlayers = playersAvaiable.Select(ix => players[ix]).ToArray();

        addQuestions(module,
           makeQuestion(Question.DumbWaitersPlayerAvailable, module, formatArgs: new[] { "was" }, correctAnswers: availablePlayers, preferredWrongAnswers: players),
           makeQuestion(Question.DumbWaitersPlayerAvailable, module, formatArgs: new[] { "was not" }, correctAnswers: players.Where(a => !availablePlayers.Contains(a)).ToArray(), preferredWrongAnswers: players));

    }
}
