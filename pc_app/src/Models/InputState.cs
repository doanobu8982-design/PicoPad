using System;
using System.Collections.Generic;

namespace GamepadEmulator.Models
{
    public enum GamepadButton
    {
        None = 0,
        Up,
        Down,
        Left,
        Right,
        RsUp,
        RsDown,
        RsLeft,
        RsRight,
        A,
        B,
        X,
        Y,
        LB,
        RB,
        L3,
        R3,
        LT,
        RT,
        Start,
        Back,
        Guide
    }

    public struct InputState : IEquatable<InputState>
    {
        public bool Up;
        public bool Down;
        public bool Left;
        public bool Right;

        public bool RsUp;
        public bool RsDown;
        public bool RsLeft;
        public bool RsRight;

        public bool A;
        public bool B;
        public bool X;
        public bool Y;
        public bool LB;
        public bool RB;
        public bool L3;
        public bool R3;

        public bool LT;
        public bool RT;

        public bool Start;
        public bool Back;
        public bool Guide;

        public void SetButton(GamepadButton button, bool pressed)
        {
            switch (button)
            {
                case GamepadButton.Up: Up = pressed; break;
                case GamepadButton.Down: Down = pressed; break;
                case GamepadButton.Left: Left = pressed; break;
                case GamepadButton.Right: Right = pressed; break;
                case GamepadButton.RsUp: RsUp = pressed; break;
                case GamepadButton.RsDown: RsDown = pressed; break;
                case GamepadButton.RsLeft: RsLeft = pressed; break;
                case GamepadButton.RsRight: RsRight = pressed; break;
                case GamepadButton.A: A = pressed; break;
                case GamepadButton.B: B = pressed; break;
                case GamepadButton.X: X = pressed; break;
                case GamepadButton.Y: Y = pressed; break;
                case GamepadButton.LB: LB = pressed; break;
                case GamepadButton.RB: RB = pressed; break;
                case GamepadButton.L3: L3 = pressed; break;
                case GamepadButton.R3: R3 = pressed; break;
                case GamepadButton.LT: LT = pressed; break;
                case GamepadButton.RT: RT = pressed; break;
                case GamepadButton.Start: Start = pressed; break;
                case GamepadButton.Back: Back = pressed; break;
                case GamepadButton.Guide: Guide = pressed; break;
            }
        }

        public void Reset()
        {
            Up = false;
            Down = false;
            Left = false;
            Right = false;
            RsUp = false;
            RsDown = false;
            RsLeft = false;
            RsRight = false;
            A = false;
            B = false;
            X = false;
            Y = false;
            LB = false;
            RB = false;
            L3 = false;
            R3 = false;
            LT = false;
            RT = false;
            Start = false;
            Back = false;
            Guide = false;
        }

        public bool Equals(InputState other)
        {
            return Up == other.Up &&
                   Down == other.Down &&
                   Left == other.Left &&
                   Right == other.Right &&
                   RsUp == other.RsUp &&
                   RsDown == other.RsDown &&
                   RsLeft == other.RsLeft &&
                   RsRight == other.RsRight &&
                   A == other.A &&
                   B == other.B &&
                   X == other.X &&
                   Y == other.Y &&
                   LB == other.LB &&
                   RB == other.RB &&
                   L3 == other.L3 &&
                   R3 == other.R3 &&
                   LT == other.LT &&
                   RT == other.RT &&
                   Start == other.Start &&
                   Back == other.Back &&
                   Guide == other.Guide;
        }

        public override bool Equals(object? obj) => obj is InputState other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(Up);
            hash.Add(Down);
            hash.Add(Left);
            hash.Add(Right);
            hash.Add(RsUp);
            hash.Add(RsDown);
            hash.Add(RsLeft);
            hash.Add(RsRight);
            hash.Add(A);
            hash.Add(B);
            hash.Add(X);
            hash.Add(Y);
            hash.Add(LB);
            hash.Add(RB);
            hash.Add(L3);
            hash.Add(R3);
            hash.Add(LT);
            hash.Add(RT);
            hash.Add(Start);
            hash.Add(Back);
            hash.Add(Guide);
            return hash.ToHashCode();
        }

        public static bool operator ==(InputState left, InputState right) => left.Equals(right);
        public static bool operator !=(InputState left, InputState right) => !left.Equals(right);
    }
}

