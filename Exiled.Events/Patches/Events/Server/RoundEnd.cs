// -----------------------------------------------------------------------
// <copyright file="RoundEnd.cs" company="Exiled Team">
// Copyright (c) Exiled Team. All rights reserved.
// Licensed under the CC BY-SA 3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace Exiled.Events.Patches.Events.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;

    using Exiled.API.Features;
    using Exiled.Events.Attributes;
    using Exiled.Events.EventArgs.Server;

    using GameCore;

    using HarmonyLib;

    using MEC;

    using PlayerRoles;

    using PluginAPI.Core;
    using PluginAPI.Events;

    using RoundRestarting;

    using UnityEngine;

    using EndingConditionState = PluginAPI.Events.RoundEndConditionsCheckCancellationData.RoundEndConditionsCheckCancellation;

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1600 // Elements should be documented

    /// <summary>
    ///     Patches <see cref="RoundSummary.Start" />.
    ///     Adds the <see cref="Handlers.Server.EndingRound" /> and <see cref="Handlers.Server.RoundEnded" /> event.
    /// </summary>
    [EventPatch(typeof(Handlers.Server), nameof(Handlers.Server.EndingRound))]
    [EventPatch(typeof(Handlers.Server), nameof(Handlers.Server.RoundEnded))]
    [HarmonyPatch(typeof(RoundSummary), nameof(RoundSummary.Start))]
    internal static class RoundEnd
    {
        private static IEnumerator<float> Process(RoundSummary roundSummary)
        {
            float time = Time.unscaledTime;
            API.Features.Round.__kIsEnded = false;
            while (roundSummary != null)
            {
                yield return Timing.WaitForSeconds(2.5f);

                if (RoundSummary.RoundLock || (roundSummary.KeepRoundOnOne && ReferenceHub.AllHubs.Count((ReferenceHub x) =>
                    x.characterClassManager.InstanceMode != ClientInstanceMode.DedicatedServer) < 2) ||
                    !RoundSummary.RoundInProgress() || Time.unscaledTime - time < 15f)
                    continue;

                RoundSummary.SumInfo_ClassList newList = default;
                foreach (ReferenceHub allHub in ReferenceHub.AllHubs)
                {
                    switch (allHub.GetTeam())
                    {
                        case Team.ClassD:
                            newList.class_ds++;
                            break;
                        case Team.ChaosInsurgency:
                            newList.chaos_insurgents++;
                            break;
                        case Team.FoundationForces:
                            newList.mtf_and_guards++;
                            break;
                        case Team.Scientists:
                            newList.scientists++;
                            break;
                        case Team.SCPs:
                            if (allHub.GetRoleId() == RoleTypeId.Scp0492)
                                newList.zombies++;
                            else
                                newList.scps_except_zombies++;

                            break;
                    }
                }

                yield return float.NegativeInfinity;
                newList.warhead_kills = AlphaWarheadController.Detonated ? AlphaWarheadController.Singleton.WarheadKills : (-1);
                yield return float.NegativeInfinity;
                int facilityForces = newList.mtf_and_guards + newList.scientists;
                int chaosInsurgency = newList.chaos_insurgents + newList.class_ds;
                int anomalies = newList.scps_except_zombies + newList.zombies;
                RoundSummary.SurvivingSCPs = newList.scps_except_zombies;
                float dEscapePercentage = (roundSummary.classlistStart.class_ds != 0) ? ((newList.class_ds + RoundSummary.EscapedClassD) / roundSummary.classlistStart.class_ds) : 0;
                float sEscapePercentage = (roundSummary.classlistStart.scientists == 0) ? 1 : ((newList.scientists + RoundSummary.EscapedScientists) / roundSummary.classlistStart.scientists);
                bool shouldRoundEnd;
                if (newList.class_ds <= 0 && facilityForces <= 0 && roundSummary.ChaosTargetCount == 0)
                {
                    shouldRoundEnd = true;
                }
                else
                {
                    int num3 = 0;
                    if (facilityForces > 0 || chaosInsurgency > 0 || anomalies > 0)
                        num3++;

                    shouldRoundEnd = num3 <= 1;
                }

                if (!roundSummary._roundEnded)
                {
                    EndingConditionState cancellation = EventManager.ExecuteEvent<RoundEndConditionsCheckCancellationData>(new RoundEndConditionsCheckEvent(shouldRoundEnd)).Cancellation;
                    if (cancellation == EndingConditionState.ConditionsSatisfied)
                    {
                        roundSummary._roundEnded = true;
                    }
                    else
                    {
                        if (cancellation == EndingConditionState.ConditionsNotSatisfied && !roundSummary._roundEnded)
                            continue;

                        if (shouldRoundEnd)
                            roundSummary._roundEnded = true;
                    }
                }

                bool anyCI = chaosInsurgency > 0;
                bool anyAnomalies = anomalies > 0;
                RoundSummary.LeadingTeam leadingTeam = RoundSummary.LeadingTeam.Draw;

                if (facilityForces > 0)
                    leadingTeam = (RoundSummary.EscapedScientists < RoundSummary.EscapedClassD) ? RoundSummary.LeadingTeam.Draw : RoundSummary.LeadingTeam.FacilityForces;
                else if (anyAnomalies || (anyAnomalies && anyCI))
                    leadingTeam = (RoundSummary.EscapedClassD > RoundSummary.SurvivingSCPs) ? RoundSummary.LeadingTeam.ChaosInsurgency : ((RoundSummary.SurvivingSCPs > RoundSummary.EscapedScientists) ? RoundSummary.LeadingTeam.Anomalies : RoundSummary.LeadingTeam.Draw);
                else if (anyCI)
                    leadingTeam = (RoundSummary.EscapedClassD >= RoundSummary.EscapedScientists) ? RoundSummary.LeadingTeam.ChaosInsurgency : RoundSummary.LeadingTeam.Draw;

                EndingRoundEventArgs endingRoundEventArgs = new(leadingTeam, newList, shouldRoundEnd);
                Handlers.Server.OnEndingRound(endingRoundEventArgs);

                roundSummary._roundEnded = endingRoundEventArgs.IsRoundEnded;
                leadingTeam = (RoundSummary.LeadingTeam)endingRoundEventArgs.LeadingTeam;

                if (!roundSummary._roundEnded)
                    continue;

                RoundEndCancellationData roundEndCancellationData = EventManager.ExecuteEvent<RoundEndCancellationData>(new RoundEndEvent(leadingTeam));
                while (roundEndCancellationData.IsCancelled)
                {
                    if (roundEndCancellationData.Delay <= 0f)
                        yield break;

                    yield return Timing.WaitForSeconds(roundEndCancellationData.Delay);
                    roundEndCancellationData = EventManager.ExecuteEvent<RoundEndCancellationData>(new RoundEndEvent(leadingTeam));
                }

                if (Statistics.FastestEndedRound.Duration > RoundStart.RoundLength)
                    Statistics.FastestEndedRound = new Statistics.FastestRound(leadingTeam, RoundStart.RoundLength, DateTime.Now);

                Statistics.CurrentRound.ClassDAlive = newList.class_ds;
                Statistics.CurrentRound.ScientistsAlive = newList.scientists;
                Statistics.CurrentRound.MtfAndGuardsAlive = newList.mtf_and_guards;
                Statistics.CurrentRound.ChaosInsurgencyAlive = newList.chaos_insurgents;
                Statistics.CurrentRound.ZombiesAlive = newList.zombies;
                Statistics.CurrentRound.ScpsAlive = newList.scps_except_zombies;
                Statistics.CurrentRound.WarheadKills = newList.warhead_kills;
                FriendlyFireConfig.PauseDetector = true;
                string text = "Round finished! Anomalies: " + anomalies + " | Chaos: " + chaosInsurgency + " | Facility Forces: " + facilityForces + " | D escaped percentage: " + dEscapePercentage + " | S escaped percentage: " + sEscapePercentage + ".";
                GameCore.Console.AddLog(text, Color.gray);
                ServerLogs.AddLog(ServerLogs.Modules.Logger, text, ServerLogs.ServerLogType.GameEvent);
                yield return Timing.WaitForSeconds(1.5f);
                int timeToRestart = Mathf.Clamp(ConfigFile.ServerConfig.GetInt("auto_round_restart_time", 10), 5, 1000);

                if (roundSummary != null)
                    roundSummary.RpcShowRoundSummary(roundSummary.classlistStart, newList, leadingTeam, RoundSummary.EscapedClassD, RoundSummary.EscapedScientists, RoundSummary.KilledBySCPs, timeToRestart, (int)RoundStart.RoundLength.TotalSeconds);

                API.Features.Round.__kIsEnded = true;
                RoundEndedEventArgs roundEndedEventArgs = new((API.Enums.LeadingTeam)leadingTeam, newList, timeToRestart);
                Handlers.Server.OnRoundEnded(roundEndedEventArgs);

                yield return Timing.WaitForSeconds(timeToRestart - 1);

                roundSummary.RpcDimScreen();
                yield return Timing.WaitForSeconds(1f);
                RoundRestart.InitiateRoundRestart();
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode != OpCodes.Call)
                {
                    yield return instruction;
                    continue;
                }

                if (instruction.operand is MethodBase methodBase && (methodBase.Name != nameof(RoundSummary._ProcessServerSideCode)))
                {
                    yield return instruction;
                    continue;
                }

                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RoundEnd), nameof(Process)));
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.FirstMethod(typeof(MECExtensionMethods2), m =>
                {
                    Type[] generics = m.GetGenericArguments();
                    ParameterInfo[] @params = m.GetParameters();

                    return (m.Name == "CancelWith") && generics.Length == 1 && @params.Length == 2 &&
                    @params[0].ParameterType == typeof(IEnumerator<float>) && @params[1].ParameterType == generics[0];
                }).MakeGenericMethod(typeof(RoundSummary)));
            }
        }
    }
}
