using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace SNSHostRules;

[BepInPlugin("org.sns.hostrules", "SNS Host Rules", "0.3.12")]
public sealed class Plugin : BasePlugin
{
    internal static Plugin Instance = null!;
    internal static bool Enabled;
    internal static readonly Dictionary<byte, int> Strikes = new();
    internal static readonly HashSet<byte> PendingKicks = new();
    internal static readonly Dictionary<byte, byte> ShapeTargets = new();
    internal static bool WasInGame;
    internal const string LobbyRules = "Rules:\n\n(Everyone)\n- Do not report bodies\n- Do not call meetings\n\n(Crewmates)\n- Complete all your tasks, even if you are dead, as that is the only way for crewmates to win\n\n(Impostors)\n- Only sabotage lights and comms\n- Do not close doors\n- Only kill if you are SS as the victim\n- Do not vent";
    internal static float LastStartAnnouncement = -1f;
    internal static float PendingKillMeetingAt = -1f;
    internal static byte PendingKillVictimId = byte.MaxValue;
    internal static byte PendingKillOffenderId = byte.MaxValue;
    internal static float LastSabotageObservedAt = -1f;
    internal static float LastIllegalKillAt = -1f;
    internal static byte LastIllegalKiller = byte.MaxValue;
    internal static byte LastIllegalVictim = byte.MaxValue;
    internal static float MeetingStartedAt = -1f;
    internal static float MeetingTotalDuration = -1f;
    internal static float MeetingDiscussionDuration = -1f;
    internal static bool ApiInventoryLogged;
    internal static bool ModStampShown;
    internal static float NextStampAttempt;
    
    internal static bool AttemptedTimedSkip;
    internal static int SkipProbeCount;
    internal static float MeetingWarningReadyAt = -1f;
    internal static float RecheckEndCriteriaAt = -1f;
    internal static bool SendingSkipResult;
    internal static bool SkipResultIssuedForMeeting;
    internal static float LastBootAttempt = -1f;
    internal static float LastMeetingEnded = -1f;
    internal static readonly Dictionary<byte, string> Names = new();
    internal static string? Warning;
    internal static float NextWarning, NextAdvert;
    internal static byte? ReportedBy;
    internal static bool SuppressEjection;
    internal static bool InternalMeetingCall;
    internal static bool ReportMeetingOpening;
    internal static float KickDueAt = -1f;
    internal static bool WasInMeeting;
    internal static bool SawMeeting;
    internal static readonly HashSet<byte> KicksAttempted = new();
    internal static bool InMeeting => MeetingHud.Instance != null;
    internal static bool IsHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    public override void Load()
    {
        Instance = this;
        Log.LogInfo("SNS Host Rules v0.3.12 starting.");
        // Register the F7/status runner BEFORE attempting any version-sensitive patches.
        // One failed Harmony patch must not make the whole plugin appear invisible.
        AddComponent<Runner>();
        Log.LogInfo("SNS Host Rules runner registered; attempting Harmony patches.");
        try
        {
            var harmony = new Harmony("org.sns.hostrules");
            Type[] patches = { typeof(ReportPatch), typeof(CmdCheckMurderPatch), typeof(HostCheckMurderPatch), typeof(ShapeshiftPatch),
                typeof(SabotagePatch), typeof(SabotageReaderPatch), typeof(DoorCloseRpcPatch),
                typeof(DoorCloseReceiverPatch), typeof(DoorCloseExecutionPatch), typeof(VentPatch), typeof(VentReaderPatch), typeof(VentOperationPatch), typeof(VentUpdateProbePatch),
                typeof(MeetingClosePatch), typeof(MeetingEndPatch), typeof(StartCountdownPatch),
                typeof(HostOnlyBroadcastVersionPatch), typeof(HostOnlyIsModdedPatch) };
            foreach (Type patch in patches)
            {
                try { harmony.CreateClassProcessor(patch).Patch(); Log.LogInfo("SNS patched: " + patch.Name); }
                catch (Exception ex) { Log.LogError("SNS patch failed: " + patch.Name + ": " + ex); }
            }
        }
        catch (Exception ex)
        {
            Log.LogError("SNS Host Rules Harmony patch failure: " + ex);
            Log.LogWarning("F7/status indicator should still work; gameplay enforcement is INCOMPLETE.");
        }
        Log.LogInfo("SNS Host Rules v0.3.12 loaded independently. F7 toggles (host only).");
    }

    internal static void Reset()
    {
        Strikes.Clear(); PendingKicks.Clear(); ShapeTargets.Clear(); Names.Clear(); LastStartAnnouncement = -1f;
        Warning = null; ReportedBy = null; SuppressEjection = false;
        InternalMeetingCall = false; ReportMeetingOpening = false; KickDueAt = -1f; WasInMeeting = false; SawMeeting = false; WasInGame = false; LastMeetingEnded = -1f; KicksAttempted.Clear(); RecheckEndCriteriaAt = -1f; SendingSkipResult = false; SkipResultIssuedForMeeting = false; LastBootAttempt = -1f; PendingKillMeetingAt = -1f; PendingKillVictimId = byte.MaxValue; PendingKillOffenderId = byte.MaxValue; LastSabotageObservedAt = -1f; AttemptedTimedSkip = false; SkipProbeCount = 0; MeetingWarningReadyAt = -1f; LastIllegalKillAt = -1f; LastIllegalKiller = byte.MaxValue; LastIllegalVictim = byte.MaxValue; MeetingStartedAt = -1f; MeetingTotalDuration = -1f; MeetingDiscussionDuration = -1f; ApiInventoryLogged = false;
    }

    // Warning chat is attributed to the warned player, not to the host. This is
    // a host-originated RPC on the player's object, not a request that the remote
    // client execute the mod. Keep rename/chat/restore in one try/finally.
    internal static void Send(string content, bool lobby = false)
    {
        if (!Enabled || !IsHost || PlayerControl.LocalPlayer == null) return;
        var host = PlayerControl.LocalPlayer;
        PlayerControl sender = host;
        if (!lobby && ReportedBy.HasValue)
        {
            var offender = FindPlayer(ReportedBy.Value);
            if (offender != null && offender.Data != null && !offender.Data.IsDead)
                sender = offender;
            else Instance.Log.LogWarning("SNS warning sender unavailable or dead; falling back to host.");
        }
        string original = Name(sender);
        try
        {
            sender.RpcSetName(lobby ? "<color=#AA1010><b>SHIFT 'N SEEK</b></color>" : "<color=#AA1010><b>SHIFT 'N SEEK WARNING</b></color>");
            bool accepted = sender.RpcSendChat(content);
            Instance.Log.LogInfo($"SNS chat accepted={accepted}; sender={sender.PlayerId}; offenderAttributed={!lobby && sender != host}; hostDead={host.Data?.IsDead}; len={content.Length}");
            if (!accepted && sender != host)
            {
                // Preserve a visible warning path if the game refuses remote
                // player attribution; do not send duplicate messages on success.
                Instance.Log.LogWarning("SNS offender-attributed chat returned false; trying host fallback.");
                string hostOriginal = Name(host);
                try
                {
                    host.RpcSetName("<color=#AA1010><b>SHIFT 'N SEEK WARNING</b></color>");
                    host.RpcSendChat(content);
                }
                finally { host.RpcSetName(hostOriginal); }
            }
        }
        catch (Exception e) { Instance.Log.LogWarning("SNS chat send failed: " + e); }
        finally
        {
            try { sender.RpcSetName(original); }
            catch (Exception e) { Instance.Log.LogWarning("SNS sender-name restoration failed: " + e); }
        }
    }

    private static string StripFormatting(string input) => System.Text.RegularExpressions.Regex.Replace(input ?? "", "<[^>]*>", "");

    internal static string Name(PlayerControl player)
    {
        try { return player.Data.PlayerName; }
        catch { return "Player " + player.PlayerId; }
    }

    // Among Us palette indices in the standard 2026 outfit ordering.
    // If reflection cannot retrieve the outfit color safely, use neutral white.
    private static readonly string[] OutfitHex = {
        "#C51111", "#132ED1", "#117F2D", "#ED54BA", "#EF7D0D", "#F5F557",
        "#3F474E", "#D6E0F0", "#6B2FBC", "#71491E", "#38FEDC", "#50EF39",
        "#6B2F3A", "#E89EC8", "#F0E6B5", "#758593", "#918877", "#EC7578"
    };

    internal static string EscapeRichText(string value) => (value ?? string.Empty)
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    internal static string PlayerHex(PlayerControl player)
    {
        try
        {
            object? data = player.Data;
            if (data == null) return "#FFFFFF";
            // Resolve through reflection because Il2CppInterop generated outfit
            // member names can differ between game versions.
            var outfitMember = data.GetType().GetProperty("DefaultOutfit")
                ?? data.GetType().GetProperty("CurrentOutfit");
            object? outfit = outfitMember?.GetValue(data);
            object? color = outfit?.GetType().GetProperty("ColorId")?.GetValue(outfit)
                ?? outfit?.GetType().GetField("ColorId")?.GetValue(outfit);
            if (color != null)
            {
                int index = Convert.ToInt32(color);
                if (index >= 0 && index < OutfitHex.Length) return OutfitHex[index];
            }
        }
        catch (Exception ex) { Instance.Log.LogDebug("SNS outfit color unavailable: " + ex.Message); }
        return "#FFFFFF";
    }

    internal static string OffenseText(string reason)
    {
        if (reason.Contains("kill", StringComparison.OrdinalIgnoreCase))
            return "Illegal kill attempt (you must SS into the victim)";
        if (reason.Contains("sabotage", StringComparison.OrdinalIgnoreCase))
            return "Illegal sabotage (only lights & comms are allowed)";
        if (reason.Contains("Reporting a body", StringComparison.OrdinalIgnoreCase))
            return "Reporting a body (do not report bodies)";
        if (reason.Contains("Calling a meeting", StringComparison.OrdinalIgnoreCase))
            return "Calling a meeting (do not call meetings)";
        return EscapeRichText(reason);
    }

    internal static string FormatWarning(PlayerControl culprit, string reason, int strikeCount)
    {
        string name = EscapeRichText(Name(culprit));
        string color = PlayerHex(culprit);
        string consequence = culprit.AmOwner ? "You have been WARNED"
            : strikeCount >= 2 ? "You will be KICKED"
            : "This is your FINAL WARNING";
        // TextMeshPro rich text. No closing 'clear' tag is needed: each
        // color/bold scope is explicitly closed to prevent style bleed.
        return $"<b>Player:</b> <color={color}>{name}</color>\n"
            + $"<b>Offense:</b> {OffenseText(reason)}\n"
            + $"<b><color=#8B0000>!!!</color> {consequence} <color=#8B0000>!!!</color></b>";
    }

    internal static void Violation(PlayerControl culprit, string reason, bool meetingAlreadyCalled = false, bool deferMeeting = false)
    {
        if (!Enabled || !IsHost || culprit == null || !InGame()) return;
        byte id = culprit.PlayerId;
        if (PendingKicks.Contains(id)) return;
        Names[id] = Name(culprit);
        int count = Strikes.TryGetValue(id, out int previous) ? previous + 1 : 1;
        Strikes[id] = count;
        bool kick = count >= 2;
        if (kick && !culprit.AmOwner) PendingKicks.Add(id);
        Warning = FormatWarning(culprit, reason, count);
        ReportedBy = id;
        NextWarning = Time.realtimeSinceStartup;
        MeetingWarningReadyAt = Time.realtimeSinceStartup;
        Instance.Log.LogInfo($"Rule violation {id}: {reason}; strike {count}");
        // NOTE: calling a meeting may fail if the game is transitioning, or
        // when a meeting is already active. In that case use current meeting.
        if (!InMeeting && !meetingAlreadyCalled && !ReportMeetingOpening && !deferMeeting)
        {
            RequestWarningMeeting(culprit, "immediate violation");
        }
    }

    // Call ReportDeadBody on the OFFENDER's player object, not the local host.
    // This is an experimental host-initiated meeting path for a dead host;
    // success is determined by whether MeetingHud actually opens.
    internal static void RequestWarningMeeting(PlayerControl offender, string source)
    {
        if (!Enabled || !IsHost || !InGame() || InMeeting || offender == null) return;
        if (offender.Data == null || offender.Data.IsDead)
        {
            Instance.Log.LogWarning($"SNS warning meeting not requested: offender {offender.PlayerId} is dead; source={source}");
            return;
        }
        InternalMeetingCall = true;
        try
        {
            Instance.Log.LogInfo($"SNS warning meeting request: reporter={offender.PlayerId}, hostDead={PlayerControl.LocalPlayer?.Data?.IsDead}, source={source}");
            offender.ReportDeadBody(null);
        }
        catch (Exception ex) { Instance.Log.LogWarning("SNS offender-report meeting request failed: " + ex); }
        finally { InternalMeetingCall = false; }
    }

    internal static PlayerControl? FindPlayer(byte id)
    {
        foreach (var player in PlayerControl.AllPlayerControls)
            if (player != null && player.PlayerId == id) return player;
        return null;
    }

    internal static void QueueKillMeeting(byte victim, byte offender)
    {
        // Illegal kills are cancelled before a death occurs. Queue an emergency
        // meeting after the current call stack completes; victim stays alive.
        PendingKillVictimId = victim;
        PendingKillOffenderId = offender;
        PendingKillMeetingAt = Time.realtimeSinceStartup + 0.75f;
        Instance.Log.LogInfo($"SNS deferred warning meeting for victim {victim}");
    }

    internal static void ScheduleKicks(string source)
    {
        if (!Enabled || !IsHost || PendingKicks.Count == 0) return;
        // Delay until meeting and transition screens settle.
        float due = Time.realtimeSinceStartup + 8f;
        if (KickDueAt < 0f || due < KickDueAt) KickDueAt = due;
        Instance.Log.LogInfo($"Pending SNS kick(s): {PendingKicks.Count}; trigger={source}");
    }

    internal static void FlushKicks()
    {
        if (!Enabled || !IsHost || InMeeting || ExileController.Instance != null) return;
        KickDueAt = -1f;
        foreach (byte id in new List<byte>(PendingKicks))
        {
            if (KicksAttempted.Contains(id)) continue;
            try
            {
                bool found = false;
                foreach (var p in PlayerControl.AllPlayerControls)
                {
                    if (p == null || p.PlayerId != id || p.Data == null) continue;
                    found = true;
                    if (p.AmOwner)
                    {
                        Instance.Log.LogWarning("Refusing to kick host/player owner.");
                        KicksAttempted.Add(id);
                        PendingKicks.Remove(id);
                        break;
                    }
                    int clientId = p.Data.ClientId;
                    Instance.Log.LogInfo($"SNS issuing host kick for player {id}, network client {clientId}");
                    AmongUsClient.Instance.KickPlayer(clientId, false);
                    Instance.Log.LogInfo($"SNS KickPlayer call returned for player {id}; remote disconnection must be verified.");
                    KicksAttempted.Add(id);
                    PendingKicks.Remove(id);
                    // The authoritative game-end check must occur AFTER the disconnect
                    // has propagated. Never fabricate a winner from local role counts.
                    RecheckEndCriteriaAt = Time.realtimeSinceStartup + 2f;
                    break;
                }
                if (!found) { Instance.Log.LogWarning($"Player {id} not found for kick; will retry."); }
            }
            catch (Exception e) { Instance.Log.LogError($"Kick for player {id} failed: {e}"); }
        }
        if (PendingKicks.Count > 0) KickDueAt = Time.realtimeSinceStartup + 3f;
        if (PendingKicks.Count == 0) { Warning = null; ReportedBy = null; }
    }

    internal static void TryRecheckEndCriteria()
    {
        RecheckEndCriteriaAt = -1f;
        if (!Enabled || !IsHost || !InGame()) return;
        try
        {
            var ship = ShipStatus.Instance;
            if (ship == null) return;
            // Dynamic lookup handles builds which expose CheckEndCriteria differently.
            var method = AccessTools.Method(ship.GetType(), "CheckEndCriteria", Type.EmptyTypes);
            if (method == null)
            {
                Instance.Log.LogWarning("SNS endgame recheck unavailable: ShipStatus.CheckEndCriteria() not exposed. Do not force a winner.");
                return;
            }
            method.Invoke(ship, null);
            Instance.Log.LogInfo("SNS requested host CheckEndCriteria after kick; verify game-end outcome separately.");
        }
        catch (Exception ex) { Instance.Log.LogWarning("SNS endgame recheck failed: " + ex); }
    }

    internal static bool InGame()
    {
        return AmongUsClient.Instance != null &&
               AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started;
    }
}

public sealed class Runner : MonoBehaviour
{
    private float noticeUntil;
    private string notice = "SNS Host Rules loaded — create a lobby as host, then press F7";
    public Runner(IntPtr ptr) : base(ptr) { }
    private static bool stampDiagnosticLogged;
    private static void EnsureModStamp()
    {
        if (Plugin.ModStampShown || Time.realtimeSinceStartup < Plugin.NextStampAttempt) return;
        Plugin.NextStampAttempt = Time.realtimeSinceStartup + 3f;
        try
        {
            // Verified against the supplied 2026.9.29 interop assembly:
            // ModManager : DestroyableSingleton<ModManager>.
            // Instance belongs to the *generic base*, NOT ModManager itself.
            // Searching ModManager.GetProperty("Instance") without
            // FlattenHierarchy missed that inherited static property.
            // Access the real generated IL2CPP singleton directly instead.
            ModManager? manager = DestroyableSingleton<ModManager>.Instance;
            if (manager == null)
            {
                if (!stampDiagnosticLogged)
                    Plugin.Instance.Log.LogWarning("SNS stamp: DestroyableSingleton<ModManager>.Instance is null; retrying.");
                stampDiagnosticLogged = true;
                return;
            }
            // ShowModStamp requires the ModStamp component created by the splash
            // setup. Don't mark success before the actual component exists.
            if (manager.ModStamp == null)
            {
                if (!stampDiagnosticLogged)
                    Plugin.Instance.Log.LogWarning("SNS stamp: ModManager is present but its ModStamp component is null; retrying.");
                stampDiagnosticLogged = true;
                return;
            }
            manager.ShowModStamp();
            Plugin.ModStampShown = true;
            Plugin.Instance.Log.LogInfo("SNS stamp: official ModManager.ShowModStamp() called on the real singleton.");
        }
        catch (Exception ex)
        {
            if (!stampDiagnosticLogged)
                Plugin.Instance.Log.LogWarning("SNS stamp: direct singleton invocation failed; retrying: " + ex);
            stampDiagnosticLogged = true;
        }
    }
    public void OnGUI()
    {
        EnsureModStamp();
        // Persistent diagnostic indicator; no dependence on game chat or Harmony hooks.
        string status = Plugin.Enabled ? "ON" : "OFF";
        string host = Plugin.IsHost ? "HOST" : "NOT HOST";
        GUI.Label(new Rect(12f, 12f, 600f, 24f), $"SNS Host Rules v0.3.12 | {status} | {host} | F7");
        // Temporary disclosure if the game's original stamp has not loaded.
        // This is NOT a substitute for the official stamped icon.
        if (!Plugin.ModStampShown)
            GUI.Box(new Rect(Screen.width - 126f, 8f, 115f, 29f), "MODDED");
        if (Time.realtimeSinceStartup < noticeUntil)
            GUI.Label(new Rect(12f, 38f, 800f, 24f), notice);
    }
    // Resolve the discussion timer. A zero-second discussion needs a small
    // grace period, otherwise the host would skip during HUD initialization.
    private static float GetDiscussionTime()
    {
        try
        {
            Type? t = AccessTools.TypeByName("GameOptionsManager");
            object? singleton = t?.GetProperty("Instance")?.GetValue(null);
            object? current = singleton?.GetType().GetProperty("CurrentGameOptions")?.GetValue(singleton);
            if (current == null) return -1f;
            object? d = current.GetType().GetProperty("DiscussionTime")?.GetValue(current);
            if (d != null) return Math.Max(0f, Convert.ToSingle(d));
            Type? names = AccessTools.TypeByName("Int32OptionNames");
            if (names != null)
            {
                var getInt = current.GetType().GetMethod("GetInt", new[] { names });
                if (getInt != null) return Math.Max(0f, Convert.ToSingle(getInt.Invoke(current,
                    new[] { Enum.Parse(names, "DiscussionTime") })));
            }
        }
        catch (Exception ex) { Plugin.Instance.Log.LogWarning("SNS discussion duration detection: " + ex.Message); }
        return -1f;
    }
    private static void Inventory(Type type, string prefix, params string[] fragments)
    {
        try
        {
            var matches = new List<string>();
            foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                     System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            {
                foreach (string fragment in fragments)
                {
                    if (m.Name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    matches.Add(m.ToString() ?? m.Name);
                    break;
                }
                if (matches.Count >= 35) break;
            }
            Plugin.Instance.Log.LogInfo("SNS API inventory " + prefix + ": " + string.Join(" || ", matches));
        }
        catch (Exception ex) { Plugin.Instance.Log.LogWarning("SNS API inventory " + prefix + " failed: " + ex.Message); }
    }
    private static void LogApiInventory()
    {
        if (Plugin.ApiInventoryLogged) return;
        Plugin.ApiInventoryLogged = true;
        Inventory(typeof(ShipStatus), "ShipStatus", "UpdateSystem", "Rpc");
        Inventory(typeof(PlayerControl), "PlayerControl", "Vent", "Sabotage", "Rpc");
        Inventory(typeof(Vent), "Vent", "Use", "Enter", "Exit");
        Inventory(typeof(MeetingHud), "MeetingHud", "Voting", "Vote", "Timer");
        Type? ventilation = AccessTools.TypeByName("VentilationSystem");
        if (ventilation != null) Inventory(ventilation, "VentilationSystem", "Update", "Boot", "Vent");
        Type? sabotage = AccessTools.TypeByName("SabotageSystemType");
        if (sabotage != null) Inventory(sabotage, "SabotageSystemType", "Update", "Sabotage", "Repair");
    }
    private void TryDiscussionSkip(float now)
    {
        var hud = MeetingHud.Instance;
        if (hud == null || !Plugin.IsHost || Plugin.AttemptedTimedSkip) return;
        float discussion = Plugin.MeetingDiscussionDuration;
        if (discussion < 0f) return;
        try
        {
            float gameClock = hud.discussionTimer;
            // In this build the HUD exposes a discussionTimer; the only safe
            // automatic trigger is a timer that visibly progresses toward
            // the discussion boundary. Log its direction before deciding.
            if (Plugin.SkipProbeCount++ % 120 == 0)
                Plugin.Instance.Log.LogInfo($"SNS meeting timer probe: hud.discussionTimer={gameClock:F2}; configured={discussion:F2}; elapsedLocal={now-Plugin.MeetingStartedAt:F2}");
            // discussionTimer normally counts upward from negative/zero
            // through discussion; using configured duration rather than the
            // initial HUD-creation timestamp avoids the observed 6s skew.
            if (gameClock < discussion - 1.25f || gameClock >= discussion) return;
            Plugin.AttemptedTimedSkip = true;
            Plugin.Instance.Log.LogWarning($"SNS authoritative-discussion skip: gameClock={gameClock:F2}, discussion={discussion:F2}");
            hud.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0L), null, true, false, ushort.MinValue);
            Plugin.Instance.Log.LogInfo("SNS discussion skip RPC returned; validate clients.");
        }
        catch (Exception ex) { Plugin.AttemptedTimedSkip = true; Plugin.Instance.Log.LogWarning("SNS discussion-clock skip failed: " + ex); }
    }
    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7))
        {
            if (!Plugin.IsHost)
            {
                notice = "F7 ignored: you are not the lobby host";
                noticeUntil = Time.realtimeSinceStartup + 8f;
                Plugin.Instance.Log.LogInfo("F7 refused: you are not host.");
                return;
            }
            Plugin.Enabled = !Plugin.Enabled;
            Plugin.Reset();
            Plugin.NextAdvert = Time.realtimeSinceStartup + 30f;
            notice = "SNS enforcement " + (Plugin.Enabled ? "ON" : "OFF");
            noticeUntil = Time.realtimeSinceStartup + 8f;
            Plugin.Instance.Log.LogInfo(notice);
        }
        if (!Plugin.Enabled) return;
        if (!Plugin.IsHost) { Plugin.Enabled = false; Plugin.Reset(); return; }
        float now = Time.realtimeSinceStartup;
        bool meetingNow = Plugin.InMeeting;
        if (meetingNow && !Plugin.WasInMeeting)
        {
            Plugin.AttemptedTimedSkip = false;
            Plugin.SkipProbeCount = 0;
            Plugin.MeetingStartedAt = now;
            Plugin.MeetingDiscussionDuration = GetDiscussionTime();
            Plugin.Instance.Log.LogInfo($"SNS meeting opened: discussion={Plugin.MeetingDiscussionDuration:F1}s; reporter={Plugin.ReportedBy?.ToString() ?? "unknown"}");
            LogApiInventory();
            if (Plugin.Warning != null) Plugin.NextWarning = now + 0.6f;
        }
        if (meetingNow) Plugin.SawMeeting = true;
        // MeetingHud.Close is not a reliable final network state across game builds.
        // Also observe the HUD transition. Do not kick while the meeting is open.
        if (Plugin.WasInMeeting && !meetingNow)
        {
            Plugin.LastMeetingEnded = now;
            if (Plugin.PendingKicks.Count > 0) Plugin.ScheduleKicks("HUD disappeared");
        }
        Plugin.WasInMeeting = meetingNow;
        if (meetingNow && !Plugin.AttemptedTimedSkip) TryDiscussionSkip(now);
        // A closed HUD is not sufficient: the exile cutscene may still be active.
        // Delay network kicks until gameplay has safely resumed.
        if (Plugin.KickDueAt >= 0f && now >= Plugin.KickDueAt && !meetingNow &&
            ExileController.Instance == null && Plugin.LastMeetingEnded >= 0f &&
            now - Plugin.LastMeetingEnded >= 8f)
            Plugin.FlushKicks();
        if (Plugin.RecheckEndCriteriaAt >= 0f && now >= Plugin.RecheckEndCriteriaAt)
            Plugin.TryRecheckEndCriteria();
        if (Plugin.PendingKillMeetingAt >= 0f && now >= Plugin.PendingKillMeetingAt)
        {
            byte victimId = Plugin.PendingKillVictimId;
            byte offenderId = Plugin.PendingKillOffenderId;
            Plugin.PendingKillMeetingAt = -1f;
            Plugin.PendingKillVictimId = byte.MaxValue;
            Plugin.PendingKillOffenderId = byte.MaxValue;
            if (Plugin.InGame() && !Plugin.InMeeting)
            {
                var offender = Plugin.FindPlayer(offenderId);
                if (offender != null) Plugin.RequestWarningMeeting(offender, $"blocked murder victim={victimId}");
                else Plugin.Instance.Log.LogWarning($"SNS meeting offender {offenderId} missing after blocked murder victim={victimId}");
            }
        }
        // Known base form on a fresh game start. Do not guess if SNS was toggled on
        // midway through an already running round.
        bool gameNow = Plugin.InGame();
        if (!Plugin.WasInGame && gameNow)
        {
            Plugin.ShapeTargets.Clear();
            foreach (var player in PlayerControl.AllPlayerControls)
                if (player != null) Plugin.ShapeTargets[player.PlayerId] = player.PlayerId;
            Plugin.Instance.Log.LogInfo($"SNS initialized baseline disguises for {Plugin.ShapeTargets.Count} players at match start.");
        }
        if (Plugin.WasInGame && !gameNow) Plugin.ShapeTargets.Clear();
        Plugin.WasInGame = gameNow;
        if (!Plugin.InGame() && !Plugin.InMeeting && now >= Plugin.NextAdvert)
        {
            Plugin.Send(Plugin.LobbyRules, lobby: true);
            Plugin.NextAdvert = now + 30f;
        }
        if (Plugin.InMeeting && Plugin.Warning != null && now >= Plugin.NextWarning)
        {
            Plugin.Send(Plugin.Warning);
            Plugin.Instance.Log.LogInfo("SNS warning chat attempted: " + Plugin.Warning);
            Plugin.NextWarning = now + 15f;
        }
    }
}

// Harmony patches below target Among Us game methods. They are deliberately
// isolated so an update can be repaired by replacing one method signature.
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
internal static class ReportPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerControl __instance, NetworkedPlayerInfo? target)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame()) return;
        // Reports with a target are body reports; null is an emergency meeting.
        // Our own rule-triggered meeting MUST NOT itself become a new strike.
        if (Plugin.InternalMeetingCall) return;
        if (__instance == null) return;
        Plugin.ReportedBy = __instance.PlayerId;
        // This event itself is opening the meeting. Never call another one.
        Plugin.ReportMeetingOpening = true;
        try { Plugin.Violation(__instance, target == null ? "Calling a meeting" : "Reporting a body", meetingAlreadyCalled: true); }
        finally { Plugin.ReportMeetingOpening = false; }
    }
}

// IMPORTANT: v0.2.5's MurderPlayer cancellation was local-only and caused
// vanilla clients to see a death while the host did not. NEVER cancel that
// method. Intercept command/check calls before any murder RPC is emitted.
internal static class KillGuard
{
    internal static bool Allow(PlayerControl killer, PlayerControl victim, string stage)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame() || killer == null || victim == null) return true;
        if (!Plugin.ShapeTargets.TryGetValue(killer.PlayerId, out byte shape))
        {
            Plugin.Instance.Log.LogWarning($"SNS {stage} unknown disguise killer={killer.PlayerId}; fail open to avoid unsynced kill state.");
            return true;
        }
        if (shape == victim.PlayerId) return true;
        float now = Time.realtimeSinceStartup;
        if (Plugin.LastIllegalKillAt >= 0 && now - Plugin.LastIllegalKillAt < 1.5f
            && Plugin.LastIllegalKiller == killer.PlayerId && Plugin.LastIllegalVictim == victim.PlayerId) return false;
        Plugin.LastIllegalKillAt = now;
        Plugin.LastIllegalKiller = killer.PlayerId; Plugin.LastIllegalVictim = victim.PlayerId;
        Plugin.Instance.Log.LogWarning($"SNS rejected illegal murder BEFORE execution: stage={stage}, killer={killer.PlayerId}, victim={victim.PlayerId}, disguise={shape}");
        Plugin.Violation(killer, "Only kill when you are SS as the victim!", deferMeeting: true);
        Plugin.QueueKillMeeting(victim.PlayerId, killer.PlayerId);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerControl), "CmdCheckMurder")]
internal static class CmdCheckMurderPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerControl __instance, PlayerControl target)
        => KillGuard.Allow(__instance, target, "CmdCheckMurder");
}

// Some AU builds process a separate host CheckMurder before RPC dispatch.
// Patch only if that method exists and returns void: skipping a non-void
// native method with an invented result would risk protocol corruption.
[HarmonyPatch]
internal static class HostCheckMurderPatch
{
    private static System.Reflection.MethodBase? TargetMethod()
    {
        foreach (var m in typeof(PlayerControl).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            if (m.Name != "CheckMurder" || m.ReturnType != typeof(void)) continue;
            var p = m.GetParameters();
            if (p.Length == 1 && p[0].ParameterType == typeof(PlayerControl)) return m;
        }
        return null;
    }
    [HarmonyPrefix]
    private static bool Prefix(PlayerControl __instance, PlayerControl target)
        => KillGuard.Allow(__instance, target, "CheckMurder");
}

[HarmonyPatch(typeof(PlayerControl), "Shapeshift")]
internal static class ShapeshiftPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerControl __instance, PlayerControl targetPlayer, bool animate)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || targetPlayer == null) return;
        Plugin.ShapeTargets[__instance.PlayerId] = targetPlayer.PlayerId;
        Plugin.Instance.Log.LogInfo($"SNS shape state: player {__instance.PlayerId} -> {targetPlayer.PlayerId} (animate={animate})");
    }
}

// Applies to the host's ShipStatus dispatch when it actually reaches this path.
// For unsupported calls we fall through instead of inventing repair state.
[HarmonyPatch(typeof(ShipStatus), "UpdateSystem", new Type[] { typeof(SystemTypes), typeof(PlayerControl), typeof(byte) })]
internal static class SabotagePatch
{
    [HarmonyPrefix]
    private static bool Prefix(SystemTypes systemType, PlayerControl player, byte amount)
        => SabotageGuard.Allow(systemType, player, amount, "ShipStatus.UpdateSystem");
}

internal static class SabotageGuard
{
    private static void InstanceDoorLog(byte player, string system, byte amount, string path, bool impostor)
    {
        Plugin.Instance.Log.LogInfo($"SNS DOOR TRACE update: player={player}, system={system}, amount={amount}, path={path}, impostor={impostor}");
    }

    internal static bool Allow(SystemTypes system, PlayerControl player, byte amount, string path)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || player == null || !Plugin.InGame()) return true;
        // Door closures are submitted as system updates by remote clients on
        // some maps, bypassing the host-only RpcCloseDoorsOfType sender hook.
        // A door-system action originating from an impostor is rejected.
        // Other players' door interactions remain untouched.
        if (system != SystemTypes.Sabotage)
        {
            var systemName = system.ToString();
            if (systemName.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bool impostor = player.Data?.Role != null && player.Data.Role.IsImpostor;
                InstanceDoorLog(player.PlayerId, systemName, amount, path, impostor);
                if (impostor)
                {
                    Plugin.Instance.Log.LogWarning($"SNS denied impostor door-system update: {systemName}, player={player.PlayerId}, amount={amount}");
                    return false;
                }
            }
            return true;
        }
        string kind = Enum.GetName(typeof(SystemTypes), (SystemTypes)amount) ?? $"unknown({amount})";
        Plugin.Instance.Log.LogInfo($"SNS sabotage request via {path}: player={player.PlayerId}, amount={amount}, system={kind}");
        if (kind.Equals("Electrical", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("Comms", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("Communications", StringComparison.OrdinalIgnoreCase)) return true;
        if (kind.StartsWith("unknown(", StringComparison.Ordinal)) return true;
        // SNS rule: illegal sabotage is silently cancelled, not a strike.
        // Do not set Warning, queue a meeting, or add to PendingKicks.
        Plugin.Instance.Log.LogWarning($"SNS silently cancelled illegal sabotage BEFORE system update: {kind}, by player {player.PlayerId}");
        return false;
    }
}

// Prevent door-closing RPCs initiated by the host's ShipStatus.
// Remote/client-initiated door closure can use other network paths; this is
// intentionally fail-safe and requires multiplayer testing.
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.RpcCloseDoorsOfType), new Type[] { typeof(SystemTypes) })]
internal static class DoorCloseRpcPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SystemTypes __0)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame()) return true;
        Plugin.Instance.Log.LogWarning($"SNS silently blocked door-closing RPC: group={__0}");
        return false;
    }
}


// The host must block incoming CloseDoorsOfType RPCs, not only its OWN
// RpcCloseDoorsOfType sender. The 2026.9.29 game's RpcCalls enum and
// ShipStatus.HandleRpc(byte, Hazel.MessageReader) are verified from the
// supplied Assembly-CSharp interop metadata. Do not consume the reader.
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc),
    new Type[] { typeof(byte), typeof(Hazel.MessageReader) })]
internal static class DoorCloseReceiverPatch
{
    [HarmonyPrefix]
    private static bool Prefix(byte __0)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame()) return true;
        if (__0 != (byte)RpcCalls.CloseDoorsOfType) return true;
        Plugin.Instance.Log.LogWarning("SNS blocked incoming door-closing RPC at ShipStatus.HandleRpc.");
        return false;
    }
}

// Protect the actual execution path as a second layer. This is safer than
// scanning and patching every native door-system UpdateSystem wrapper (the
// dynamic scan in v0.3.8 crashed IL2CPP during startup).
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CloseDoorsOfType),
    new Type[] { typeof(SystemTypes) })]
internal static class DoorCloseExecutionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SystemTypes __0)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame()) return true;
        Plugin.Instance.Log.LogWarning($"SNS blocked door-closing execution at ShipStatus.CloseDoorsOfType: group={__0}");
        return false;
    }
}

// Disabled since v0.3.8.1: the reflection/Harmony door-system patch caused
// a fatal IL2CPP AccessViolationException during early class initialization.
// Use the existing ShipStatus guards until a safe typed hook is validated.

// The 2026.9.29 IL2CPP build uses a Hazel.MessageReader, NOT a byte.
// Inspect the reader non-destructively; if its position cannot be restored,
// let vanilla handle it instead of corrupting the packet stream.
internal static class HazelBytePeek
{
    internal static bool TryRead(Hazel.MessageReader reader, out byte amount)
    {
        amount = 0;
        try
        {
            var type = reader.GetType();
            var property = type.GetProperty("Position");
            var field = type.GetField("Position");
            if (property == null && field == null) return false;
            object? old = property != null ? property.GetValue(reader) : field!.GetValue(reader);
            if (old == null) return false;
            if (property != null && !property.CanWrite) return false;
            amount = reader.ReadByte();
            if (property != null) property.SetValue(reader, old);
            else field!.SetValue(reader, old);
            return true;
        }
        catch (Exception ex) { Plugin.Instance.Log.LogWarning("SNS Hazel peek unavailable: " + ex.Message); return false; }
    }
}

[HarmonyPatch(typeof(ShipStatus), "UpdateSystem", new Type[] { typeof(SystemTypes), typeof(PlayerControl), typeof(Hazel.MessageReader) })]
internal static class SabotageReaderPatch
{
    [HarmonyPrefix]
    private static bool Prefix(SystemTypes __0, PlayerControl __1, Hazel.MessageReader __2)
    {
        if (Plugin.Enabled && Plugin.IsHost && Plugin.InGame() && __1 != null && __1.Data?.Role != null && __1.Data.Role.IsImpostor)
        {
            string incoming = __0.ToString();
            if (incoming.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
                Plugin.Instance.Log.LogInfo($"SNS remote door request seen: player={__1.PlayerId}, system={incoming}");
        }
        // Safe read-only telemetry at an existing working ShipStatus hook.
        // This will tell us whether the remote closure ever comes through this path.
        if (Plugin.Enabled && Plugin.IsHost && Plugin.InGame() && __1 != null &&
            __1.Data?.Role != null && __1.Data.Role.IsImpostor && __0 != SystemTypes.Sabotage)
        {
            Plugin.Instance.Log.LogInfo($"SNS system update trace: player={__1.PlayerId}, system={__0}, path=reader");
        }
        if (__0 != SystemTypes.Sabotage && __0.ToString().IndexOf("Door", StringComparison.OrdinalIgnoreCase) < 0) return true;
        if (!HazelBytePeek.TryRead(__2, out byte amount)) return true;
        return __1 == null || SabotageGuard.Allow(__0, __1, amount, "ShipStatus.UpdateSystem(reader)");
    }
}

// Observe local vent entry; incoming system guard below handles host requests.
[HarmonyPatch(typeof(Vent), "EnterVent")]
internal static class VentPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Vent __instance, PlayerControl pc)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame() || pc == null || pc.Data?.Role == null || !pc.Data.Role.IsImpostor) return true;
        Plugin.Instance.Log.LogInfo($"SNS vent animation path: player={pc.PlayerId}, vent={__instance.Id}; central boot guard active.");
        return true;
    }
}

// Host-side incoming ventilation requests, not local exit animations.
// The peeked first byte may be a sequence identifier, not an operation; diagnostic only.
// Unknown packets are passed unchanged to prevent trapping players or desynchronization.
[HarmonyPatch(typeof(VentilationSystem), "UpdateSystem", new Type[] { typeof(PlayerControl), typeof(Hazel.MessageReader) })]
internal static class VentReaderPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PlayerControl __0, Hazel.MessageReader __1)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame() || __0 == null || __0.Data?.Role == null || !__0.Data.Role.IsImpostor) return true;
        if (!HazelBytePeek.TryRead(__1, out byte op)) return true;
        var kind = Enum.GetName(typeof(VentilationSystem.Operation), (VentilationSystem.Operation)op) ?? $"unknown({op})";
        Plugin.Instance.Log.LogInfo($"SNS incoming vent request player={__0.PlayerId}, op={op}, kind={kind}");
        if (kind.Contains("Enter", StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Instance.Log.LogInfo($"SNS observed incoming vent entry player={__0.PlayerId}, op={kind}; central boot handles recovery.");
            return true;
        }
        return true;
    }
}

// v0.3.4: Intercept the central ventilation state transition. We deliberately
// patch by method NAME and type-safe *leading* positional arguments, so the
// generated IL2CPP SequenceBuffer<T> parameter need not be re-declared here.
// The patch must install successfully on the host; check SNS patched line.
[HarmonyPatch(typeof(VentilationSystem), "PerformVentOp")]
internal static class VentOperationPatch
{
    private static bool Booting;
    private static readonly Dictionary<byte, float> LastAttempt = new();

    // Let vanilla register its entry before attempting a synchronized recovery.
    // Cancelling only the host's entry (v0.3.6) did not stop remote clients.
    [HarmonyPostfix]
    private static void Postfix(VentilationSystem __instance, byte __0, VentilationSystem.Operation __1, byte __2)
    {
        if (Booting || !Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame() || __instance == null) return;
        if (!__1.ToString().Equals("Enter", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            PlayerControl? player = null;
            foreach (var candidate in PlayerControl.AllPlayerControls)
                if (candidate != null && candidate.PlayerId == __0) { player = candidate; break; }
            if (player?.Data?.Role == null || !player.Data.Role.IsImpostor) return;

            float now = Time.realtimeSinceStartup;
            if (LastAttempt.TryGetValue(__0, out float last) && now - last < 0.25f) return;
            LastAttempt[__0] = now;
            Plugin.Instance.Log.LogWarning($"SNS VENT BOOT attempting: player={__0}, vent={__2}, inVent={player.inVent}");
            Booting = true;
            try
            {
                __instance.BootImpostorFromVent((int)__2, __0);
                Plugin.Instance.Log.LogInfo($"SNS VENT BOOT method returned: player={__0}, vent={__2}; verify remote synchronization");
            }
            finally { Booting = false; }
        }
        catch (Exception ex)
        {
            Booting = false;
            Plugin.Instance.Log.LogWarning("SNS VENT BOOT failed safely: " + ex);
        }
    }
}

// v0.3.12 diagnostic only. This overload might carry host-side ventilation
// transitions that bypass PerformVentOp. Never suppress it without confirming
// sequence semantics, or vanilla peers could become desynchronized.
[HarmonyPatch(typeof(VentilationSystem), "Update", new Type[] { typeof(VentilationSystem.Operation), typeof(int) })]
internal static class VentUpdateProbePatch
{
    [HarmonyPrefix]
    private static void Prefix(VentilationSystem.Operation __0, int __1)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || !Plugin.InGame()) return;
        Plugin.Instance.Log.LogInfo($"SNS VENT TRACE Update op={__0}, index={__1}");
    }
}

// v0.3.4 does NOT patch VotingComplete or RpcVotingComplete. Rewriting
// these methods caused dead/alive desync, lost Proceed buttons, and black screens.
// The runner attempts one standalone skip RPC with 1 second on the visible timer.

[HarmonyPatch(typeof(MeetingHud), "Close")]
internal static class MeetingClosePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Plugin.Enabled || !Plugin.IsHost) return;
        Plugin.AttemptedTimedSkip = false;
        Plugin.ScheduleKicks("MeetingHud.Close");
        Plugin.Warning = null;
        Plugin.ReportedBy = null;
    }
}

[HarmonyPatch(typeof(ExileController), "WrapUp")]
internal static class MeetingEndPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!Plugin.Enabled || !Plugin.IsHost) return;
        Plugin.ScheduleKicks("ExileController.WrapUp");
    }
}

// Host's Start action. Patch only the actual BeginGame entry point, and do not
// suppress any vanilla checks, confirmation dialogs or start logic.
[HarmonyPatch(typeof(GameStartManager), "BeginGame")]
internal static class StartCountdownPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameStartManager __instance)
    {
        if (!Plugin.Enabled || !Plugin.IsHost || __instance == null || Plugin.InGame()) return;
        try
        {
            // Countdowns are host-authoritative; only extend if vanilla really
            // transitioned into countdown. Otherwise don't announce prematurely.
            if (__instance.startState != GameStartManager.StartingStates.Countdown) return;
            float now = Time.realtimeSinceStartup;
            if (Plugin.LastStartAnnouncement > 0f && now - Plugin.LastStartAnnouncement < 1.5f) return;
            Plugin.LastStartAnnouncement = now;
            __instance.countDownTimer = 10.0001f;
            Plugin.Send(Plugin.LobbyRules, lobby: true);
            Plugin.NextAdvert = now + 30f;
            Plugin.Instance.Log.LogInfo("SNS host Start: rules announced; countdown extended to 10 seconds.");
        }
        catch (Exception e) { Plugin.Instance.Log.LogWarning("SNS host Start announcement/countdown failed safely: " + e); }
    }
}


// ---------------- Official server: HOST-ONLY +25 registration ----------------
// Per Innersloth's AmongUsModdingInformation technical documentation, adding 25
// to the int32 protocol/broadcast version on an online connection activates
// host-authority mode. This is NOT the all-client AU MCI GUID mechanism.
// Both exact methods and return types were verified in the 2026.9.29
// Assembly-CSharp.dll supplied with the project.
// The implementation follows the published TownOfHost ConstantsPatch example.
internal static class HostOnlyRegistration
{
    internal const int ModdedVersionOffset = 25;
    private static bool loggedVersion;

    // An explicit local-game exception preserves practice/local behavior.
    // When the client singleton doesn't exist (e.g., early connection setup),
    // do not skip registration; the protocol must be set BEFORE lobby creation.
    internal static bool ApplyForCurrentConnection()
    {
        try
        {
            var client = AmongUsClient.Instance;
            if (client == null) return true;
            string mode = client.NetworkMode.ToString();
            return !string.Equals(mode, "LocalGame", StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    internal static void LogFirstVersion(int vanilla, int modded)
    {
        if (loggedVersion) return;
        loggedVersion = true;
        try
        {
            Plugin.Instance?.Log.LogInfo(
                $"SNS +25 registration: protocol {vanilla} -> {modded} " +
                "(host-only authority flag; official-server acceptance requires testing).");
        }
        catch { /* logging must never break a network version calculation */ }
    }
}

[HarmonyPatch(typeof(Constants), nameof(Constants.GetBroadcastVersion))]
internal static class HostOnlyBroadcastVersionPatch
{
    private static void Postfix(ref int __result)
    {
        if (!HostOnlyRegistration.ApplyForCurrentConnection()) return;
        int vanilla = __result;
        __result = vanilla + HostOnlyRegistration.ModdedVersionOffset;
        HostOnlyRegistration.LogFirstVersion(vanilla, __result);
    }
}

// Companion version-mod status hook used by the established TownOfHost host-only
// implementation. It prevents game-side modded-version checks disagreeing with
// the broadcast version we send to the official servers.
[HarmonyPatch(typeof(Constants), nameof(Constants.IsVersionModded))]
internal static class HostOnlyIsModdedPatch
{
    private static bool Prefix(ref bool __result)
    {
        if (!HostOnlyRegistration.ApplyForCurrentConnection()) return true;
        __result = true;
        return false;
    }
}
