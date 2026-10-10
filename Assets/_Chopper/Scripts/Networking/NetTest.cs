#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using Chopper.Core;
using Chopper.Enemies;
using Chopper.Player;
using Chopper.Trees;
using Chopper.World;
using UnityEngine;

namespace Chopper.Networking
{
    // Development only: a two process multiplayer test. Start one process with
    //   -chopperHost -chopperNetTest host
    // and a second one with
    //   -chopperJoin 127.0.0.1 -chopperNetTest client
    // The host performs actions (repair the hut, fell a tree, plant saplings, skip to night, spawn an enemy, hurt the
    // other player, hand out the axe, grant a boon) and the client logs what it sees, so a script can compare the two.
    public static class NetTest
    {
        private const string Arg = "-chopperNetTest";
        private const float WaitForPlayers = 90f;
        private const float StepSeconds = 1.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Arg);
            if (index < 0 || index + 1 >= args.Length) return;

            var go = new GameObject("NetTest");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<Runner>();
            runner.StartCoroutine(args[index + 1] == "host" ? Host() : Client());
        }

        private class Runner : MonoBehaviour { }

        private static void Log(string role, string message) => Debug.Log($"[Net{role}] {message}");

        // ---------- Host ----------

        private static IEnumerator Host()
        {
            float deadline = Time.unscaledTime + WaitForPlayers;
            while (Time.unscaledTime < deadline && PlayerWood.Active.Count < 2) yield return null;
            Log("Host", $"players connected: {PlayerWood.Active.Count}");
            if (PlayerWood.Active.Count < 2)
            {
                Log("Host", "FAIL the second player never joined");
                Application.Quit(1);
                yield break;
            }
            yield return new WaitForSeconds(StepSeconds);

            PlayerWood hostPlayer = PlayerWood.Local;
            PlayerWood remote = null;
            foreach (PlayerWood p in PlayerWood.Active) if (p != hostPlayer) remote = p;
            GameConfig config = TreeManager.Instance.Config;
            TreeDatabase db = TreeManager.Instance.Database;
            EnemyDirector director = EnemyDirector.Instance;
            DayNightCycle cycle = DayNightCycle.Instance;
            Hut hut = Hut.Instance;

            Log("Host", "step 1: fell a tree, the client should see a stump");
            int treeId = 0;
            for (int i = 0; i < db.Count; i++) if (db.Get(i).Size == TreeSize.Small) { treeId = i; break; }
            Vector3 aim = Vector3.forward;
            Vector3 origin = db.Get(treeId).Position - aim * 1.5f;
            for (int i = 0; i < config.GetHitsToFell(TreeSize.Small); i++) TreeManager.Instance.ServerChop(hostPlayer, origin, aim);
            yield return new WaitForSeconds(StepSeconds);

            Log("Host", "step 2: plant saplings (a Fire forest tree falls by day)");
            for (int i = 0; i < 6; i++) director.ServerOnTreeHit(new Vector3(0f, 0f, -90f), Element.Fire, TreeSize.Large, hostPlayer);
            yield return new WaitForSeconds(StepSeconds);

            Log("Host", "step 2b: interest management, an enemy far from everyone is not sent to the client");
            Treant far = director.ServerSpawn(Element.Water, TreeSize.Small, false, new Vector3(0f, 0f, 600f));
            Treant near = director.ServerSpawn(Element.Water, TreeSize.Small, false, remote.transform.position + new Vector3(0f, 0f, 8f));
            yield return new WaitForSeconds(StepSeconds);
            bool farHidden = !far.NetworkObject.Observers.Contains(remote.Owner);
            bool nearSeen = near != null && near.NetworkObject.Observers.Contains(remote.Owner);
            Log("Host", $"interest: far enemy hidden from the client {farHidden}, a near one seen {nearSeen}");
            far.ServerDespawn();
            near.ServerDespawn();
            if (!farHidden || !nearSeen)
            {
                Log("Host", "FAIL interest management");
                Application.Quit(1);
                yield break;
            }

            Log("Host", "step 3: repair the hut and give the remote player the axe");
            hut.ServerSetRepaired(true);
            remote.GetComponent<PlayerAxe>().ServerSetHasAxe(true);
            yield return new WaitForSeconds(StepSeconds);

            Log("Host", "step 4: skip to night");
            for (int i = 0; i < 4 && !cycle.IsNight; i++) cycle.ServerSkipPhase();
            yield return new WaitForSeconds(StepSeconds * 2f);

            Log("Host", "step 5: an awake enemy next to the remote player, then it hurts them");
            Vector3 spot = remote.transform.position + new Vector3(3f, 0f, 0f);
            Treant enemy = director.ServerSpawn(Element.Water, TreeSize.Small, false, spot);
            enemy.ServerActivate();
            remote.GetComponent<PlayerHealth>().ServerDamage(30);
            yield return new WaitForSeconds(StepSeconds);

            Log("Host", "step 6: grant everyone a permanent boon");
            foreach (PlayerHealth p in PlayerHealth.All) p.GetComponent<PlayerBoons>().ServerGrant(BoonKind.Stamina);
            yield return new WaitForSeconds(StepSeconds);

            Log("Host", "step 7: dawn");
            for (int i = 0; i < 3 && cycle.IsNight; i++) cycle.ServerSkipPhase();
            yield return new WaitForSeconds(StepSeconds * 2f);

            Log("Host", $"remote health on the server: {remote.GetComponent<PlayerHealth>().Health}");
            Log("Host", "DONE");
            yield return new WaitForSeconds(3f);
            Application.Quit(0);
        }

        // ---------- Client ----------

        private static IEnumerator Client()
        {
            float deadline = Time.unscaledTime + WaitForPlayers;
            while (Time.unscaledTime < deadline && PlayerWood.Local == null) yield return null;
            if (PlayerWood.Local == null)
            {
                Log("Client", "FAIL never got a player");
                Application.Quit(1);
                yield break;
            }

            bool everSawHost = false;
            int quietSeconds = 0;
            while (true)
            {
                yield return new WaitForSeconds(1f);
                PlayerWood local = PlayerWood.Local;
                if (local == null)
                {
                    Log("Client", "disconnected");
                    Application.Quit(0);
                    yield break;
                }

                int saplings = 0, awake = 0, elemental = 0;
                foreach (Treant t in Treant.ClientAll)
                {
                    if (t == null) continue;
                    if (t.IsActive) awake++;
                    else if (!t.IsBoss)
                    {
                        saplings++;
                        if (t.Element != Element.Neutral) elemental++;
                    }
                }

                everSawHost |= PlayerWood.Active.Count >= 2;
                DayNightCycle cycle = DayNightCycle.Instance;
                Log("Client", string.Format(CultureInfo.InvariantCulture,
                    "players={0} stumps={1} saplings={2} elemental={3} awake={4} phase={5} hutRepaired={6} hasAxe={7} health={8} boons={9}",
                    PlayerWood.Active.Count, TreeManager.Instance.StumpCount, saplings, elemental, awake,
                    cycle != null ? cycle.Phase.ToString() : "none", Hut.Instance != null && Hut.Instance.IsRepaired,
                    local.GetComponent<PlayerAxe>().HasAxe, PlayerHealth.Local != null ? PlayerHealth.Local.Health : -1,
                    local.GetComponent<PlayerBoons>().Packed));

                quietSeconds++;
                if (quietSeconds > 120) { Application.Quit(0); yield break; }
            }
        }
    }
}
#endif
