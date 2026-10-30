using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>SaveService + LocalFileStore: atomic writes, backup fallback, autosave throttling.</summary>
    public class SaveServiceTests
    {
        private const long T0 = 1_761_700_000_000;

        private string _dir;
        private BalanceConfig _balance;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "RadiantOrchardSaveTests", Guid.NewGuid().ToString("N"));
            _balance = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset").Balance;
            Assert.IsNotNull(_balance);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private SaveService NewService(ISaveStore store, long now = T0) => new SaveService(store, () => now, 3f);

        // ---------- real files ----------

        [Test]
        public void SaveThenLoadRoundTripsThroughFiles()
        {
            var store = new LocalFileStore(_dir);
            var first = NewService(store);
            var state = first.Load(_balance);
            Assert.AreEqual(SaveLoadSource.New, first.LastLoadSource);
            state.Seeds = 340;
            state.Plots[1].FruitId = "pineapple";
            Assert.IsTrue(first.SaveNow());

            Assert.IsTrue(File.Exists(store.MainPath), "save.json should exist.");
            Assert.IsFalse(File.Exists(store.TempPath), "save.tmp should not be left behind.");

            var second = NewService(new LocalFileStore(_dir));
            var loaded = second.Load(_balance);
            Assert.AreEqual(SaveLoadSource.Main, second.LastLoadSource);
            Assert.AreEqual(340, loaded.Seeds);
            Assert.AreEqual("pineapple", loaded.Plots[1].FruitId);
            Assert.AreEqual(T0, loaded.LastSaveUtc);
        }

        [Test]
        public void SecondSaveKeepsThePreviousOneAsBackup()
        {
            var store = new LocalFileStore(_dir);
            var service = NewService(store);
            var state = service.Load(_balance);
            state.Seeds = 10;
            service.SaveNow();
            state.Seeds = 20;
            service.SaveNow();

            Assert.IsTrue(File.Exists(store.BackupPath), "save.bak should exist after the second save.");
            StringAssert.Contains("\"_seeds\":10", store.ReadBackup());
            StringAssert.Contains("\"_seeds\":20", store.Read());
        }

        [Test]
        public void CorruptMainLoadsFromBackupAndMarksDirty()
        {
            var store = new LocalFileStore(_dir);
            var service = NewService(store);
            var state = service.Load(_balance);
            state.Seeds = 77;
            service.SaveNow();
            service.SaveNow(); // now save.bak holds the 77-seed state too
            File.WriteAllText(store.MainPath, "{ \"_seeds\": 5, broken");

            LogAssert.Expect(LogType.Warning, new Regex("not valid JSON"));
            LogAssert.Expect(LogType.Warning, new Regex("restored from the backup"));
            var recovered = NewService(new LocalFileStore(_dir));
            var loaded = recovered.Load(_balance);

            Assert.AreEqual(SaveLoadSource.Backup, recovered.LastLoadSource);
            Assert.AreEqual(77, loaded.Seeds);
            Assert.IsTrue(recovered.IsDirty, "A recovered state should be written back to the main file.");
        }

        [Test]
        public void NothingReadableStartsANewGame()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, LocalFileStore.MainFileName), "not json");
            File.WriteAllText(Path.Combine(_dir, LocalFileStore.BackupFileName), "");

            LogAssert.Expect(LogType.Warning, new Regex("not valid JSON"));
            var service = NewService(new LocalFileStore(_dir));
            var state = service.Load(_balance);

            Assert.AreEqual(SaveLoadSource.New, service.LastLoadSource);
            Assert.AreEqual(0, state.Seeds);
            Assert.AreEqual(9, state.Plots.Count);
        }

        [Test]
        public void DeleteRemovesAllSaveFiles()
        {
            var store = new LocalFileStore(_dir);
            var service = NewService(store);
            service.Load(_balance);
            service.SaveNow();
            service.SaveNow();
            store.Delete();
            Assert.IsNull(store.Read());
            Assert.IsNull(store.ReadBackup());
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        // ---------- versions and repair ----------

        [Test]
        public void NewerSaveVersionIsNotUsed()
        {
            var fake = new FakeStore();
            var future = GameState.CreateNew(_balance);
            future.Version = GameState.CurrentVersion + 1;
            future.Seeds = 999;
            fake.Main = JsonUtility.ToJson(future);

            LogAssert.Expect(LogType.Warning, new Regex("newer than this build"));
            LogAssert.Expect(LogType.Warning, new Regex("will not save"));
            var service = NewService(fake);
            var state = service.Load(_balance);

            Assert.AreEqual(SaveLoadSource.New, service.LastLoadSource);
            Assert.AreEqual(0, state.Seeds);
        }

        [Test]
        public void NewerSaveIsNeverOverwritten()
        {
            var fake = new FakeStore();
            var future = GameState.CreateNew(_balance);
            future.Version = GameState.CurrentVersion + 1;
            future.Seeds = 999;
            string futureJson = JsonUtility.ToJson(future);
            fake.Main = futureJson;

            LogAssert.Expect(LogType.Warning, new Regex("newer than this build"));
            LogAssert.Expect(LogType.Warning, new Regex("will not save"));
            var service = NewService(fake);
            var state = service.Load(_balance);
            Assert.IsTrue(service.WritesBlocked);

            state.Seeds = 5;
            service.MarkDirty();
            Assert.IsFalse(service.Tick(0f), "A new game must not autosave over a newer save.");
            Assert.IsFalse(service.Tick(10f));
            Assert.IsFalse(service.SaveNow(), "Pause/quit must not save over a newer save either.");

            Assert.AreEqual(0, fake.Writes);
            Assert.AreEqual(futureJson, fake.Main);
        }

        [Test]
        public void NewerMainWithOlderBackupLoadsBackupButNeverWrites()
        {
            var fake = new FakeStore();
            var future = GameState.CreateNew(_balance);
            future.Version = GameState.CurrentVersion + 1;
            future.Seeds = 999;
            string futureJson = JsonUtility.ToJson(future);
            fake.Main = futureJson;
            var older = GameState.CreateNew(_balance);
            older.Seeds = 50;
            fake.Backup = JsonUtility.ToJson(older);

            LogAssert.Expect(LogType.Warning, new Regex("newer than this build"));
            LogAssert.Expect(LogType.Warning, new Regex("restored from the backup"));
            LogAssert.Expect(LogType.Warning, new Regex("will not save"));
            var service = NewService(fake);
            var state = service.Load(_balance);

            Assert.AreEqual(SaveLoadSource.Backup, service.LastLoadSource);
            Assert.AreEqual(50, state.Seeds);
            Assert.IsFalse(service.Tick(0f), "Writing the backup back would push the newer save out.");
            Assert.AreEqual(0, fake.Writes);
            Assert.AreEqual(futureJson, fake.Main);
        }

        [Test]
        public void CurrentVersionSaveIsWritable()
        {
            var fake = new FakeStore { Main = JsonUtility.ToJson(GameState.CreateNew(_balance)) };
            var service = NewService(fake);
            service.Load(_balance);

            Assert.IsFalse(service.WritesBlocked);
            Assert.IsTrue(service.SaveNow());
        }

        [Test]
        public void MissingPlotsAreRestoredOnLoad()
        {
            var fake = new FakeStore { Main = "{\"_version\":1,\"_seeds\":42,\"_plots\":[{\"_unlocked\":true}]}" };
            var service = NewService(fake);
            var state = service.Load(_balance);

            Assert.AreEqual(SaveLoadSource.Main, service.LastLoadSource);
            Assert.AreEqual(42, state.Seeds);
            Assert.AreEqual(9, state.Plots.Count);
            Assert.IsTrue(state.Plots[0].Unlocked);
            Assert.IsFalse(state.Plots[8].Unlocked);
            Assert.IsNotNull(state.Settings);
        }

        // ---------- autosave ----------

        [Test]
        public void TickOnlyWritesWhenDirtyAndIntervalPassed()
        {
            var fake = new FakeStore();
            var service = NewService(fake);
            service.Load(_balance); // a new game is dirty
            Assert.IsTrue(service.Tick(0f), "First tick of a new game should save.");
            Assert.AreEqual(1, fake.Writes);

            service.MarkDirty();
            Assert.IsFalse(service.Tick(1f), "Within 3 s of the last write.");
            Assert.IsTrue(service.Tick(3.5f), "Interval passed.");
            Assert.AreEqual(2, fake.Writes);

            Assert.IsFalse(service.Tick(10f), "Nothing changed since, so no write.");
            Assert.AreEqual(2, fake.Writes);
        }

        [Test]
        public void SaveNowStampsTheSaveTime()
        {
            var fake = new FakeStore();
            var service = NewService(fake, now: T0 + 5000);
            var state = service.Load(_balance);
            service.SaveNow();
            Assert.AreEqual(T0 + 5000, state.LastSaveUtc);
            StringAssert.Contains((T0 + 5000).ToString(), fake.Main);
        }

        [Test]
        public void FailedWriteKeepsChangesUnsaved()
        {
            var fake = new FakeStore { FailWrites = true };
            var service = NewService(fake);
            service.Load(_balance);

            LogAssert.Expect(LogType.Error, new Regex("saving failed"));
            Assert.IsFalse(service.SaveNow());
            Assert.IsTrue(service.IsDirty);
        }

        private sealed class FakeStore : ISaveStore
        {
            public string Main;
            public string Backup;
            public int Writes;
            public bool FailWrites;

            public string Read() => Main;
            public string ReadBackup() => Backup;

            public void Write(string json)
            {
                if (FailWrites) throw new IOException("disk full");
                Backup = Main;
                Main = json;
                Writes++;
            }

            public void Delete()
            {
                Main = null;
                Backup = null;
            }
        }
    }
}
