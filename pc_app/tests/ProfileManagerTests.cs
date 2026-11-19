using System;
using System.Collections.Generic;
using System.IO;
using GamepadEmulator.Core;
using GamepadEmulator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GamepadEmulator.Tests
{
    [TestClass]
    public class ProfileManagerTests
    {
        private string _testDir = null!;
        private string _testJsonPath = null!;

        [TestInitialize]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "GamepadEmulatorTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _testJsonPath = Path.Combine(_testDir, "test_profiles.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, recursive: true);
                }
            }
            catch
            {
                // Ignore test cleanup errors
            }
        }

        [TestMethod]
        public void LoadProfiles_WhenFileDoesNotExist_CreatesInitialDefault()
        {
            var manager = new ProfileManager(_testJsonPath);

            Assert.IsTrue(File.Exists(_testJsonPath), "profiles.json should be created automatically.");
            Assert.AreEqual(1, manager.Profiles.Count);
            Assert.IsNotNull(manager.ActiveProfile);
            Assert.AreEqual("デフォルト", manager.ActiveProfile.Name);
            Assert.IsFalse(manager.ActiveProfile.IsStickMode, "デフォルト profile should default to D-Pad (IsStickMode = false).");
            Assert.IsTrue(manager.ActiveProfile.Bindings.ContainsKey("Up"));

            // Up should contain both W (0x57) and Space (0x20)
            CollectionAssert.Contains(manager.ActiveProfile.Bindings["Up"], 0x57);
            CollectionAssert.Contains(manager.ActiveProfile.Bindings["Up"], 0x20);
        }

        [TestMethod]
        public void LoadProfiles_WhenProfilesEmpty_AllowsZeroProfilesWithoutAutoRecreation()
        {
            // JSON containing 0 profiles
            string emptyProfilesJson = @"{
  ""ActiveProfileId"": """",
  ""Profiles"": []
}";
            File.WriteAllText(_testJsonPath, emptyProfilesJson);

            var manager = new ProfileManager(_testJsonPath);

            // 0 profiles must be preserved without auto-generating default
            Assert.AreEqual(0, manager.Profiles.Count);
            Assert.IsNull(manager.ActiveProfile);
        }

        [TestMethod]
        public void CreateProfile_AddsAndPersistsNewProfile()
        {
            var manager = new ProfileManager(_testJsonPath);

            var created = manager.CreateProfile("SF6カスタム");

            Assert.AreEqual(2, manager.Profiles.Count);
            Assert.AreEqual(created.Id, manager.ActiveProfile!.Id);
            Assert.AreEqual("SF6カスタム", manager.ActiveProfile.Name);

            // Re-load from disk to verify persistence
            var reloadedManager = new ProfileManager(_testJsonPath);
            Assert.AreEqual(2, reloadedManager.Profiles.Count);
            Assert.AreEqual("SF6カスタム", reloadedManager.ActiveProfile!.Name);
        }

        [TestMethod]
        public void CreateProfile_WhenZeroProfiles_CreatesFromDefaultTemplate()
        {
            string emptyProfilesJson = @"{ ""ActiveProfileId"": """", ""Profiles"": [] }";
            File.WriteAllText(_testJsonPath, emptyProfilesJson);

            var manager = new ProfileManager(_testJsonPath);
            Assert.AreEqual(0, manager.Profiles.Count);
            Assert.IsNull(manager.ActiveProfile);

            var created = manager.CreateProfile("初プロファイル");

            Assert.AreEqual(1, manager.Profiles.Count);
            Assert.IsNotNull(manager.ActiveProfile);
            Assert.AreEqual("初プロファイル", manager.ActiveProfile.Name);
            Assert.IsTrue(manager.ActiveProfile.Bindings.ContainsKey("Up"));
        }

        [TestMethod]
        public void RenameProfile_ModifiesNameAndPersists()
        {
            var manager = new ProfileManager(_testJsonPath);
            string id = manager.ActiveProfile!.Id;

            bool renamed = manager.RenameProfile(id, "変更後名称");

            Assert.IsTrue(renamed);
            Assert.AreEqual("変更後名称", manager.ActiveProfile.Name);

            var reloaded = new ProfileManager(_testJsonPath);
            Assert.AreEqual("変更後名称", reloaded.ActiveProfile!.Name);
        }

        [TestMethod]
        public void CreateProfile_DuplicateName_ThrowsExceptionOrFails()
        {
            var manager = new ProfileManager(_testJsonPath);
            Assert.AreEqual(1, manager.Profiles.Count);
            Assert.AreEqual("デフォルト", manager.ActiveProfile!.Name);

            // 1. Exact match duplicate
            Assert.ThrowsExactly<ArgumentException>(() => manager.CreateProfile("デフォルト"));

            // 2. Case-insensitive duplicate (e.g. "DEFAULT" or "default")
            manager.CreateProfile("SF6");
            Assert.ThrowsExactly<ArgumentException>(() => manager.CreateProfile("sf6"));
            Assert.ThrowsExactly<ArgumentException>(() => manager.CreateProfile("SF6"));

            // 3. Trimmed whitespace duplicate
            Assert.ThrowsExactly<ArgumentException>(() => manager.CreateProfile("  SF6  "));
            Assert.ThrowsExactly<ArgumentException>(() => manager.CreateProfile("  デフォルト  "));

            // 4. Empty or whitespace-only name
            Assert.ThrowsExactly<ArgumentException>(() => manager.CreateProfile(""));
            Assert.ThrowsExactly<ArgumentException>(() => manager.CreateProfile("   "));

            // Total profiles should still be 2 ("デフォルト" and "SF6")
            Assert.AreEqual(2, manager.Profiles.Count);
        }

        [TestMethod]
        public void RenameProfile_DuplicateNameOfOtherProfile_Fails()
        {
            var manager = new ProfileManager(_testJsonPath);
            var p2 = manager.CreateProfile("StreetFighter");

            // Try to rename "StreetFighter" to "デフォルト" (already exists on profile 1)
            bool renamed1 = manager.RenameProfile(p2.Id, "デフォルト");
            Assert.IsFalse(renamed1, "Renaming to existing profile name must return false.");
            Assert.AreEqual("StreetFighter", p2.Name, "Name must remain unchanged.");

            // Try duplicate of other profile with case-difference / whitespace
            var p3 = manager.CreateProfile("GGST");
            bool renamed2 = manager.RenameProfile(p3.Id, "StreetFighter");
            Assert.IsFalse(renamed2, "Renaming to another profile name must fail.");
            Assert.AreEqual("GGST", p3.Name);

            bool renamedCase = manager.RenameProfile(p3.Id, "  streetfighter  ");
            Assert.IsFalse(renamedCase, "Renaming with case/whitespace difference of other profile name must fail.");
            Assert.AreEqual("GGST", p3.Name);
        }

        [TestMethod]
        public void RenameProfile_SameNameAsSelf_Succeeds()
        {
            var manager = new ProfileManager(_testJsonPath);
            var p2 = manager.CreateProfile("SF6");

            // Renaming to exact same name
            bool renamedSame = manager.RenameProfile(p2.Id, "SF6");
            Assert.IsTrue(renamedSame, "Renaming to own exact name must succeed.");
            Assert.AreEqual("SF6", p2.Name);

            // Renaming with case difference on self (e.g. "sf6")
            bool renamedCase = manager.RenameProfile(p2.Id, "sf6");
            Assert.IsTrue(renamedCase, "Changing own case must succeed.");
            Assert.AreEqual("sf6", p2.Name);

            // Renaming with trimmed spaces on self
            bool renamedTrim = manager.RenameProfile(p2.Id, "  sf6  ");
            Assert.IsTrue(renamedTrim, "Changing own name with surrounding spaces must succeed.");
            Assert.AreEqual("sf6", p2.Name);
        }

        [TestMethod]
        public void DeleteProfile_CanDeleteDownToZeroProfiles()
        {
            var manager = new ProfileManager(_testJsonPath);
            Assert.AreEqual(1, manager.Profiles.Count);

            string defaultId = manager.ActiveProfile!.Id;
            bool deleted = manager.DeleteProfile(defaultId);

            Assert.IsTrue(deleted, "Deleting down to 0 profiles must be allowed.");
            Assert.AreEqual(0, manager.Profiles.Count);
            Assert.IsNull(manager.ActiveProfile);

            // Persist verification
            var reloaded = new ProfileManager(_testJsonPath);
            Assert.AreEqual(0, reloaded.Profiles.Count);
            Assert.IsNull(reloaded.ActiveProfile);
        }

        [TestMethod]
        public void DeleteProfile_WhenMultipleExist_RemovesTargetAndSwitchesActive()
        {
            var manager = new ProfileManager(_testJsonPath);
            var p2 = manager.CreateProfile("プロファイル2");
            Assert.AreEqual(2, manager.Profiles.Count);
            Assert.AreEqual(p2.Id, manager.ActiveProfile!.Id);

            bool deleted = manager.DeleteProfile(p2.Id);

            Assert.IsTrue(deleted);
            Assert.AreEqual(1, manager.Profiles.Count);
            Assert.AreNotEqual(p2.Id, manager.ActiveProfile!.Id);
            Assert.AreEqual("デフォルト", manager.ActiveProfile.Name);
        }

        [TestMethod]
        public void DeleteProfile_ImmediatelyRemovesFromFile_AndDoesNotLeaveZombieData()
        {
            var manager = new ProfileManager(_testJsonPath);
            var p1 = manager.CreateProfile("テストプロファイル");
            Assert.AreEqual(2, manager.Profiles.Count);

            // Verify it was written to file
            string jsonBefore = File.ReadAllText(_testJsonPath);
            Assert.IsTrue(jsonBefore.Contains(p1.Id));
            Assert.IsTrue(jsonBefore.Contains("テストプロファイル"));

            // Delete profile
            bool deleted = manager.DeleteProfile(p1.Id);
            Assert.IsTrue(deleted);

            // Directly read the file immediately after DeleteProfile without calling SaveProfiles
            string jsonAfter = File.ReadAllText(_testJsonPath);
            Assert.IsFalse(jsonAfter.Contains(p1.Id), "Deleted profile ID must not exist in file.");
            Assert.IsFalse(jsonAfter.Contains("テストプロファイル"), "Deleted profile Name must not exist in file.");

            // Reload from disk to verify complete eradication
            var reloaded = new ProfileManager(_testJsonPath);
            Assert.AreEqual(1, reloaded.Profiles.Count);
            Assert.IsFalse(reloaded.Profiles.Any(p => p.Id == p1.Id));
        }

        [TestMethod]
        public void CreateProfile_WithSameNameAfterDeletion_GeneratesFreshDefaultBindings()
        {
            var manager = new ProfileManager(_testJsonPath);
            var p1 = manager.CreateProfile("CUSTOM_PROFILE");

            // Customize bindings heavily: A key -> 0x5A (Z key), Up -> empty
            p1.Bindings["A"] = new List<int> { 0x5A }; // Z key instead of J (0x4A)
            p1.Bindings["Up"].Clear();
            p1.IsStickMode = true;
            manager.SaveProfiles();

            // Verify customization persisted
            var reloaded = new ProfileManager(_testJsonPath);
            var found = reloaded.Profiles.First(p => p.Id == p1.Id);
            Assert.AreEqual(0x5A, found.Bindings["A"][0]);
            Assert.AreEqual(0, found.Bindings["Up"].Count);
            Assert.IsTrue(found.IsStickMode);

            // Delete the profile
            bool deleted = manager.DeleteProfile(p1.Id);
            Assert.IsTrue(deleted);

            // Immediately create a new profile with the EXACT SAME NAME
            var p2 = manager.CreateProfile("CUSTOM_PROFILE");

            // Verify p2 is a clean new instance
            Assert.AreNotEqual(p1.Id, p2.Id, "New profile must have a fresh unique ID.");
            Assert.IsFalse(p2.IsStickMode, "New profile must have default IsStickMode = false.");
            Assert.AreEqual(0x4A, p2.Bindings["A"][0], "A button must have standard default (J key = 0x4A), not customized 0x5A.");
            CollectionAssert.Contains(p2.Bindings["Up"], 0x57, "Up must have default W key (0x57).");
            CollectionAssert.Contains(p2.Bindings["Up"], 0x20, "Up must have default Space key (0x20).");

            // Verify file persistence also reflects fresh default
            var reloadedAfterNew = new ProfileManager(_testJsonPath);
            var newLoaded = reloadedAfterNew.Profiles.First(p => p.Id == p2.Id);
            Assert.AreEqual(0x4A, newLoaded.Bindings["A"][0]);
            CollectionAssert.Contains(newLoaded.Bindings["Up"], 0x57);
        }

        [TestMethod]
        public void CorruptedJson_RecoversWithDefaultAndCreatesBakFile()
        {
            File.WriteAllText(_testJsonPath, "{ this is invalid json content !@#$");

            var manager = new ProfileManager(_testJsonPath);

            Assert.AreEqual(1, manager.Profiles.Count);
            Assert.AreEqual("デフォルト", manager.ActiveProfile!.Name);

            string bakPath = _testJsonPath + ".bak";
            Assert.IsTrue(File.Exists(bakPath), "A .bak file must be created when recovering from corrupted JSON.");
            Assert.AreEqual("{ this is invalid json content !@#$", File.ReadAllText(bakPath));
        }

        [TestMethod]
        public void Migration_OldJsonFormat_MigratesToNewFormatSeamlessly()
        {
            // Simulate older JSON format with Dictionary<string, int> and ModeOn
            string legacyJson = @"{
  ""ActiveProfileId"": ""p-legacy-1"",
  ""Profiles"": [
    {
      ""Id"": ""p-legacy-1"",
      ""Name"": ""デフォルト"",
      ""ModeOn"": true,
      ""Bindings"": {
        ""Up"": 87,
        ""Down"": 83,
        ""LB"": 0
      }
    }
  ]
}";
            File.WriteAllText(_testJsonPath, legacyJson);

            var manager = new ProfileManager(_testJsonPath);

            Assert.AreEqual(1, manager.Profiles.Count);
            var active = manager.ActiveProfile!;
            Assert.AreEqual("デフォルト", active.Name);
            Assert.IsTrue(active.IsStickMode, "ModeOn: true should map to IsStickMode: true.");

            // Up should be list [87]
            Assert.IsTrue(active.Bindings.ContainsKey("Up"));
            Assert.AreEqual(1, active.Bindings["Up"].Count);
            Assert.AreEqual(87, active.Bindings["Up"][0]);

            // LB was 0 (unassigned) -> empty list
            Assert.IsTrue(active.Bindings.ContainsKey("LB"));
            Assert.AreEqual(0, active.Bindings["LB"].Count);
        }

        [TestMethod]
        public void Clone_PerformsDeepCopy_AllowsSafeRevert()
        {
            var original = GamepadProfile.CreateDefault();
            var snapshot = original.Clone();

            Assert.AreEqual(original.Id, snapshot.Id);
            Assert.AreEqual(original.Name, snapshot.Name);
            Assert.AreEqual(original.IsStickMode, snapshot.IsStickMode);

            // Mutate original
            original.IsStickMode = true;
            original.Bindings["Up"].Add(0x99);
            original.Bindings["A"] = new List<int> { 0x88 };

            // Snapshot must be untouched
            Assert.IsFalse(snapshot.IsStickMode);
            Assert.IsFalse(snapshot.Bindings["Up"].Contains(0x99));
            Assert.AreEqual(0x4A, snapshot.Bindings["A"][0]);

            // Revert original from snapshot
            original.IsStickMode = snapshot.IsStickMode;
            original.Bindings.Clear();
            foreach (var kvp in snapshot.Bindings)
            {
                original.Bindings[kvp.Key] = new List<int>(kvp.Value);
            }

            Assert.IsFalse(original.IsStickMode);
            Assert.IsFalse(original.Bindings["Up"].Contains(0x99));
            Assert.AreEqual(0x4A, original.Bindings["A"][0]);
        }

        [TestMethod]
        public void RawInputReceiver_SingleKeyAssignedToMultipleButtons_BothTurnOnAndOff()
        {
            using var receiver = new RawInputReceiver();
            var profile = new GamepadProfile
            {
                Name = "同一キー複数ボタンテスト",
                Bindings = new Dictionary<string, List<int>>
                {
                    // Assign Space (0x20) to BOTH Up and A
                    { "Up", new List<int> { 0x20 } },
                    { "A", new List<int> { 0x20 } }
                }
            };

            receiver.SetProfile(profile);

            byte[] lastPacket = new byte[6];
            InputState lastState = default;
            receiver.OnPacketGenerated += (ReadOnlySpan<byte> packet, in InputState state) =>
            {
                packet.CopyTo(lastPacket);
                lastState = state;
            };

            // 1. Press Space (0x20) -> Both Up and A must turn ON simultaneously
            receiver.HandleKeyEvent(0x20, isMake: true);
            Assert.IsTrue(receiver.CurrentRawState.Up, "Up raw state must be ON when Space is pressed.");
            Assert.IsTrue(receiver.CurrentRawState.A, "A raw state must be ON when Space is pressed.");
            Assert.IsTrue(lastState.Up, "Up cleaned state must be ON when Space is pressed.");
            Assert.IsTrue(lastState.A, "A cleaned state must be ON when Space is pressed.");

            // Byte 1 (D-Pad): Up is bit 0 (0x01)
            Assert.AreEqual(0x01, lastPacket[1] & 0x01, "Packet byte 1 must have D-Pad Up bit set.");
            // Byte 2 (Actions): A is bit 0 (0x01)
            Assert.AreEqual(0x01, lastPacket[2] & 0x01, "Packet byte 2 must have Action A bit set.");

            // 2. Release Space (0x20) -> Both Up and A must turn OFF simultaneously
            receiver.HandleKeyEvent(0x20, isMake: false);
            Assert.IsFalse(receiver.CurrentRawState.Up, "Up raw state must be OFF when Space is released.");
            Assert.IsFalse(receiver.CurrentRawState.A, "A raw state must be OFF when Space is released.");
            Assert.IsFalse(lastState.Up, "Up cleaned state must be OFF when Space is released.");
            Assert.IsFalse(lastState.A, "A cleaned state must be OFF when Space is released.");

            Assert.AreEqual(0x00, lastPacket[1] & 0x01, "Packet byte 1 D-Pad Up bit must be cleared.");
            Assert.AreEqual(0x00, lastPacket[2] & 0x01, "Packet byte 2 Action A bit must be cleared.");
        }

        [TestMethod]
        public void RawInputReceiver_MultiButton_WithSharedKeyAndIndividualKeys_ORLogic()
        {
            using var receiver = new RawInputReceiver();
            var profile = new GamepadProfile
            {
                Name = "OR・複数ボタン併用テスト",
                Bindings = new Dictionary<string, List<int>>
                {
                    { "Up", new List<int> { 0x57, 0x20 } }, // W and Space
                    { "A", new List<int> { 0x20 } }          // Space only
                }
            };

            receiver.SetProfile(profile);

            // 1. Press W (0x57) -> Up should be true, A false
            receiver.HandleKeyEvent(0x57, isMake: true);
            Assert.IsTrue(receiver.CurrentRawState.Up);
            Assert.IsFalse(receiver.CurrentRawState.A);

            // 2. Press Space (0x20) while W is held -> Up true, A becomes true
            receiver.HandleKeyEvent(0x20, isMake: true);
            Assert.IsTrue(receiver.CurrentRawState.Up);
            Assert.IsTrue(receiver.CurrentRawState.A);

            // 3. Release Space (0x20) -> Up MUST remain true because W is still held; A becomes false
            receiver.HandleKeyEvent(0x20, isMake: false);
            Assert.IsTrue(receiver.CurrentRawState.Up, "Up must remain ON because W is still held (OR logic).");
            Assert.IsFalse(receiver.CurrentRawState.A, "A must turn OFF because Space was released.");

            // 4. Release W (0x57) -> Up turns false
            receiver.HandleKeyEvent(0x57, isMake: false);
            Assert.IsFalse(receiver.CurrentRawState.Up);
            Assert.IsFalse(receiver.CurrentRawState.A);
        }

        [TestMethod]
        public void SocdCleaner_DeadMansCheck_MultiBindings_ClearsAllButtonsOnRelease()
        {
            var cleaner = new SocdCleaner();
            InputState rawState = default;
            rawState.Up = true;
            rawState.A = true;

            var multiBindings = new Dictionary<int, List<GamepadButton>>
            {
                { 0x20, new List<GamepadButton> { GamepadButton.Up, GamepadButton.A } }
            };

            HashSet<int> pressedKeys = new() { 0x20 }; // Space is recorded as pressed

            // Simulate GetAsyncKeyState returning 0 (physically released)
            Func<int, short> mockRelease = vKey => 0;

            bool changed = cleaner.PerformDeadMansCheck(ref rawState, multiBindings, pressedKeys, mockRelease);

            Assert.IsTrue(changed, "Dead man's check must report state change.");
            Assert.IsFalse(rawState.Up, "Up must be cleared.");
            Assert.IsFalse(rawState.A, "A must be cleared.");
            Assert.AreEqual(0, pressedKeys.Count, "Pressed keys set must be empty.");
        }

        [TestMethod]
        public void SocdCleaner_DeadMansCheck_MultiBindings_ORLogicMaintainsActiveButton()
        {
            var cleaner = new SocdCleaner();
            InputState rawState = default;
            rawState.Up = true;
            rawState.A = true;

            var multiBindings = new Dictionary<int, List<GamepadButton>>
            {
                { 0x57, new List<GamepadButton> { GamepadButton.Up } },
                { 0x20, new List<GamepadButton> { GamepadButton.Up, GamepadButton.A } }
            };

            HashSet<int> pressedKeys = new() { 0x57, 0x20 }; // W and Space both pressed

            // Simulate Space (0x20) released, but W (0x57) still held
            Func<int, short> mockKeyCheck = vKey => (vKey == 0x57) ? unchecked((short)0x8000) : (short)0;

            bool changed = cleaner.PerformDeadMansCheck(ref rawState, multiBindings, pressedKeys, mockKeyCheck);

            Assert.IsTrue(changed, "Dead man's check must detect Space release.");
            Assert.IsTrue(rawState.Up, "Up must remain ON because W is still held.");
            Assert.IsFalse(rawState.A, "A must be turned OFF because Space was released.");
            Assert.IsTrue(pressedKeys.Contains(0x57), "W must remain in pressedKeys.");
            Assert.IsFalse(pressedKeys.Contains(0x20), "Space must be removed from pressedKeys.");
        }

        [TestMethod]
        public void RawInputReceiver_OpposingDirections_SOCDNeutral()
        {
            using var receiver = new RawInputReceiver();
            var profile = new GamepadProfile
            {
                Name = "上下対向割り当てテスト",
                Bindings = new Dictionary<string, List<int>>
                {
                    { "Up", new List<int> { 0x20 } },
                    { "Down", new List<int> { 0x20 } }
                }
            };

            receiver.SetProfile(profile);

            InputState lastCleanedState = default;
            receiver.OnPacketGenerated += (ReadOnlySpan<byte> packet, in InputState state) =>
            {
                lastCleanedState = state;
            };

            // Press Space (bound to both Up and Down)
            receiver.HandleKeyEvent(0x20, isMake: true);
            Assert.IsTrue(receiver.CurrentRawState.Up, "Raw Up must be ON.");
            Assert.IsTrue(receiver.CurrentRawState.Down, "Raw Down must be ON.");

            // CPT Compliant SOCD Neutral: cleaned state must neutralize both Up and Down
            Assert.IsFalse(lastCleanedState.Up, "SOCD Cleaned Up must be neutral (OFF).");
            Assert.IsFalse(lastCleanedState.Down, "SOCD Cleaned Down must be neutral (OFF).");

            // Release Space
            receiver.HandleKeyEvent(0x20, isMake: false);
            Assert.IsFalse(receiver.CurrentRawState.Up);
            Assert.IsFalse(receiver.CurrentRawState.Down);
            Assert.IsFalse(lastCleanedState.Up);
            Assert.IsFalse(lastCleanedState.Down);
        }

        [TestMethod]
        public void RawInputReceiver_MultipleKeysForOneButton_ORLogic()
        {
            using var receiver = new RawInputReceiver();
            var profile = new GamepadProfile
            {
                Name = "ORテスト",
                Bindings = new Dictionary<string, List<int>>
                {
                    { "Up", new List<int> { 0x57, 0x20 } } // W and Space
                }
            };

            receiver.SetProfile(profile);

            // 1. Press W (0x57) -> Up should be true
            receiver.HandleKeyEvent(0x57, isMake: true);
            Assert.IsTrue(receiver.CurrentRawState.Up, "Up must be true when W is pressed.");

            // 2. Press Space (0x20) while W is held -> Up should still be true
            receiver.HandleKeyEvent(0x20, isMake: true);
            Assert.IsTrue(receiver.CurrentRawState.Up, "Up must remain true when both W and Space are pressed.");

            // 3. Release W (0x57) -> Up should STILL be true because Space is still held
            receiver.HandleKeyEvent(0x57, isMake: false);
            Assert.IsTrue(receiver.CurrentRawState.Up, "Up must remain true because Space is still held (OR logic).");

            // 4. Release Space (0x20) -> Up should now be false
            receiver.HandleKeyEvent(0x20, isMake: false);
            Assert.IsFalse(receiver.CurrentRawState.Up, "Up must be false after both W and Space are released.");
        }

        [TestMethod]
        public void RawInputReceiver_NullProfile_SafeNeutralNoExceptions()
        {
            using var receiver = new RawInputReceiver();

            // Set profile to null (simulating 0 profiles)
            receiver.SetProfile(null);

            // Key press events must not throw and state must remain neutral
            receiver.HandleKeyEvent(0x57, isMake: true);
            Assert.IsFalse(receiver.CurrentRawState.Up);
            Assert.IsFalse(receiver.CurrentRawState.A);

            receiver.HandleKeyEvent(0x57, isMake: false);
            Assert.IsFalse(receiver.CurrentRawState.Up);
        }

        [TestMethod]
        public void DuplicateProfile_DeepCopiesSettingsAndRemainsIndependent()
        {
            var manager = new ProfileManager(_testJsonPath);
            var source = manager.CreateProfile("SF6ベース");
            source.IsStickMode = true;
            source.Bindings["Up"] = new List<int> { 0x57, 0x20 }; // W, Space
            source.Bindings["A"] = new List<int> { 0x4A, 0x5A };  // J, Z
            manager.SaveProfiles();

            var duplicated = manager.DuplicateProfile(source, "  SF6複製  ");

            Assert.AreEqual(3, manager.Profiles.Count);
            Assert.AreEqual("SF6複製", duplicated.Name);
            Assert.AreNotEqual(source.Id, duplicated.Id);
            Assert.AreEqual(duplicated.Id, manager.ActiveProfile!.Id);
            Assert.IsTrue(duplicated.IsStickMode);
            CollectionAssert.AreEqual(new List<int> { 0x57, 0x20 }, duplicated.Bindings["Up"]);
            CollectionAssert.AreEqual(new List<int> { 0x4A, 0x5A }, duplicated.Bindings["A"]);

            // Mutate source after duplication; duplicated must remain unaffected
            source.IsStickMode = false;
            source.Bindings["Up"].Add(0x26);
            source.Bindings["A"].Clear();

            Assert.IsTrue(duplicated.IsStickMode);
            CollectionAssert.AreEqual(new List<int> { 0x57, 0x20 }, duplicated.Bindings["Up"]);
            CollectionAssert.AreEqual(new List<int> { 0x4A, 0x5A }, duplicated.Bindings["A"]);
        }

        [TestMethod]
        public void DuplicateProfile_PersistsToJsonAndReloadsCorrectly()
        {
            var manager = new ProfileManager(_testJsonPath);
            var source = manager.ActiveProfile!;
            source.IsStickMode = true;
            source.Bindings["B"] = new List<int> { 0x4B, 0x4C };
            manager.SaveProfiles();

            var duplicated = manager.DuplicateProfile(source, "非想天則用");

            var reloaded = new ProfileManager(_testJsonPath);
            Assert.AreEqual(2, reloaded.Profiles.Count);
            Assert.AreEqual(duplicated.Id, reloaded.ActiveProfile!.Id);
            Assert.AreEqual("非想天則用", reloaded.ActiveProfile.Name);
            Assert.IsTrue(reloaded.ActiveProfile.IsStickMode);
            CollectionAssert.AreEqual(new List<int> { 0x4B, 0x4C }, reloaded.ActiveProfile.Bindings["B"]);
        }

        [TestMethod]
        public void DuplicateProfile_DuplicateOrEmptyName_ThrowsArgumentException()
        {
            var manager = new ProfileManager(_testJsonPath);
            var source = manager.CreateProfile("MyProfile");
            Assert.AreEqual(2, manager.Profiles.Count);

            // Duplicate exact, case-insensitive, and whitespace-padded names
            Assert.ThrowsExactly<ArgumentException>(() => manager.DuplicateProfile(source, "MyProfile"));
            Assert.ThrowsExactly<ArgumentException>(() => manager.DuplicateProfile(source, "myprofile"));
            Assert.ThrowsExactly<ArgumentException>(() => manager.DuplicateProfile(source, "  MYPROFILE  "));
            Assert.ThrowsExactly<ArgumentException>(() => manager.DuplicateProfile(source, "デフォルト"));

            // Empty or whitespace-only names
            Assert.ThrowsExactly<ArgumentException>(() => manager.DuplicateProfile(source, ""));
            Assert.ThrowsExactly<ArgumentException>(() => manager.DuplicateProfile(source, "   "));

            Assert.AreEqual(2, manager.Profiles.Count);
        }

        [TestMethod]
        public void DuplicateProfile_NullSource_ThrowsArgumentNullException()
        {
            var manager = new ProfileManager(_testJsonPath);
            Assert.ThrowsExactly<ArgumentNullException>(() => manager.DuplicateProfile(null!, "CopyName"));
            Assert.AreEqual(1, manager.Profiles.Count);
        }
    }
}
