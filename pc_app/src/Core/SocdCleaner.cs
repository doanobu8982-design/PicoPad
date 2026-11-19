using System;
using System.Collections.Generic;
using GamepadEmulator.Models;

namespace GamepadEmulator.Core
{
    public class SocdCleaner
    {
        private bool _lastModeOn;
        private bool _isModeInitialized;

        public bool SafeNeutralRequired { get; set; }

        public void Clean(ref InputState state)
        {
            // CPT Compliant Neutral:
            // Left + Right -> Neutral (both off)
            if (state.Left && state.Right)
            {
                state.Left = false;
                state.Right = false;
            }

            // Up + Down -> Neutral (both off)
            if (state.Up && state.Down)
            {
                state.Up = false;
                state.Down = false;
            }

            // Right stick neutral
            if (state.RsLeft && state.RsRight)
            {
                state.RsLeft = false;
                state.RsRight = false;
            }

            if (state.RsUp && state.RsDown)
            {
                state.RsUp = false;
                state.RsDown = false;
            }
        }

        public InputState CleanCopy(in InputState rawState)
        {
            InputState state = rawState;
            Clean(ref state);
            return state;
        }

        public bool NotifyMode(bool modeOn)
        {
            if (!_isModeInitialized)
            {
                _isModeInitialized = true;
                _lastModeOn = modeOn;
                return false;
            }

            if (_lastModeOn != modeOn)
            {
                _lastModeOn = modeOn;
                SafeNeutralRequired = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Dead man's check: cross-checks pressed keys with GetAsyncKeyState.
        /// If key is physically released, clears the button state and returns true if any change occurred.
        /// </summary>
        public bool PerformDeadMansCheck(
            ref InputState rawState,
            IReadOnlyDictionary<int, GamepadButton> keyBindings,
            HashSet<int> pressedPhysicalKeys)
        {
            return PerformDeadMansCheck(ref rawState, keyBindings, pressedPhysicalKeys, Win32Native.GetAsyncKeyState);
        }

        public bool PerformDeadMansCheck(
            ref InputState rawState,
            IReadOnlyDictionary<int, List<GamepadButton>> multiBindings,
            HashSet<int> pressedPhysicalKeys)
        {
            return PerformDeadMansCheck(ref rawState, multiBindings, pressedPhysicalKeys, Win32Native.GetAsyncKeyState);
        }

        public bool PerformDeadMansCheck(
            ref InputState rawState,
            IReadOnlyDictionary<int, List<GamepadButton>> multiBindings,
            HashSet<int> pressedPhysicalKeys,
            Func<int, short> getAsyncKeyStateFunc)
        {
            bool stateChanged = false;
            List<int>? releasedKeys = null;

            foreach (int vKey in pressedPhysicalKeys)
            {
                bool isDown = (getAsyncKeyStateFunc(vKey) & 0x8000) != 0;

                if (!isDown)
                {
                    releasedKeys ??= new List<int>();
                    releasedKeys.Add(vKey);
                }
            }

            if (releasedKeys != null && releasedKeys.Count > 0)
            {
                // 1. Remove ALL physically released keys first so that remaining checks evaluate only truly held keys
                foreach (int vKey in releasedKeys)
                {
                    pressedPhysicalKeys.Remove(vKey);
                }

                // 2. For every released key, evaluate ALL bound buttons to completion
                foreach (int vKey in releasedKeys)
                {
                    if (multiBindings.TryGetValue(vKey, out var buttons))
                    {
                        foreach (var button in buttons)
                        {
                            bool stillPressed = false;
                            foreach (int pk in pressedPhysicalKeys)
                            {
                                if (multiBindings.TryGetValue(pk, out var bList) && bList.Contains(button))
                                {
                                    stillPressed = true;
                                    break;
                                }
                            }

                            if (!stillPressed)
                            {
                                rawState.SetButton(button, false);
                                stateChanged = true;
                            }
                        }
                    }
                }
            }

            // Safety check: if no physical keys are pressed, raw state must be fully neutral
            if (pressedPhysicalKeys.Count == 0)
            {
                InputState neutral = default;
                if (!rawState.Equals(neutral))
                {
                    rawState.Reset();
                    stateChanged = true;
                }
            }

            return stateChanged;
        }

        public bool PerformDeadMansCheck(
            ref InputState rawState,
            IReadOnlyDictionary<int, GamepadButton> keyBindings,
            HashSet<int> pressedPhysicalKeys,
            Func<int, short> getAsyncKeyStateFunc)
        {
            var multi = new Dictionary<int, List<GamepadButton>>(keyBindings.Count);
            foreach (var kvp in keyBindings)
            {
                multi[kvp.Key] = new List<GamepadButton> { kvp.Value };
            }
            return PerformDeadMansCheck(ref rawState, multi, pressedPhysicalKeys, getAsyncKeyStateFunc);
        }
    }
}

