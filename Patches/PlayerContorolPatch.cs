using Hazel;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace TownOfHost
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    class MurderPlayerPatch
    {
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost)
                return;
            Logger.info($"発生: {__instance.name}({__instance.getCustomRole()}) => {target.name}({target.getCustomRole()})","MurderPlayer");
            if(__instance == target && __instance.getCustomRole() == CustomRoles.Sheriff)
            {
                main.ps.setDeathReason(__instance.PlayerId, PlayerState.DeathReason.Suicide);
            }
            else
            {
                main.ps.setDeathReason(target.PlayerId, PlayerState.DeathReason.Kill);
            }
            //When Bait is killed
            if (target.getCustomRole() == CustomRoles.Bait && __instance.PlayerId != target.PlayerId)
            {
                Logger.SendToFile(target.name + "はBaitだった");
                new LateTask(() => __instance.CmdReportDeadBody(target.Data), 0.15f, "Bait Self Report");
            }
            else
            //Terrorist
            if (target.isTerrorist())
            {
                Logger.SendToFile(target.name + "はTerroristだった");
                main.CheckTerroristWin(target.Data);
            }
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
    class CheckMurderPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            if (!AmongUsClient.Instance.AmHost) return false;
            Logger.info($"発生: {__instance.name}({__instance.getCustomRole()}) => {target.name}({target.getCustomRole()})","CheckMerder");
            if(main.IsHideAndSeek && main.HideAndSeekKillDelayTimer > 0) {
                Logger.info("HideAndSeekの待機時間中だったため、キルをキャンセルしました。");
                return false;
            }

            if(main.BlockKilling.TryGetValue(__instance.PlayerId, out bool isBlocked) && isBlocked){
                Logger.info("キルをブロックしました。");
                return false;
            }

            main.BlockKilling[__instance.PlayerId] = true;
            if (__instance.isMafia())
            {
                if (!CustomRoles.Mafia.CanUseKillButton())
                {
                    Logger.SendToFile(__instance.name + "はMafiaだったので、キルはキャンセルされました。");
                    main.BlockKilling[__instance.PlayerId] = false;
                    return false;
                } else {
                    Logger.SendToFile(__instance.name + "はMafiaですが、他のインポスターがいないのでキルが許可されました。");
                }
            }
            if(__instance.isSheriff()) {
                if(__instance.Data.IsDead) {
                    main.BlockKilling[__instance.PlayerId] = false;
                    return false;
                }

                if(!target.canBeKilledBySheriff()) {
                    __instance.RpcMurderPlayer(__instance);
                    return false;
                }
            }
            if(target.isMadGuardian()) {
                var isTaskFinished = true;
                foreach(var task in target.Data.Tasks) {
                    if(!task.Complete) {
                        isTaskFinished = false;
                        break;
                    }
                }
                if(isTaskFinished) {
                    int dataCountBefore = NameColorManager.Instance.NameColors.Count;

                    NameColorManager.Instance.RpcAdd(__instance.PlayerId, target.PlayerId, "#ff0000");
                    if(main.MadGuardianCanSeeWhoTriedToKill)
                        NameColorManager.Instance.RpcAdd(target.PlayerId, __instance.PlayerId, "#ff0000");
                    
                    main.BlockKilling[__instance.PlayerId] = false;
                    if(dataCountBefore != NameColorManager.Instance.NameColors.Count)
                        main.NotifyRoles();
                    return false;
                }
            }
            if (__instance.isWitch())
            {
                if(__instance.GetKillOrSpell() && !main.SpelledPlayer.Contains(target))
                {
                    __instance.RpcGuardAndKill(target);
                    main.SpelledPlayer.Add(target);
                    RPCProcedure.RpcDoSpell(target.PlayerId);
                }
                main.KillOrSpell[__instance.PlayerId] = !__instance.GetKillOrSpell();
                main.NotifyRoles();
                __instance.SyncKillOrSpell();
            }
            if (__instance.isVampire() && !target.isBait())
            { //キルキャンセル&自爆処理
                __instance.RpcProtectPlayer(target, 0);
                new LateTask(() => {
                    if (target.protectedByGuardian)
                    {
                        //通常はこちら
                        __instance.RpcMurderPlayer(target);
                    }
                    else
                    {
                        //会議開始と同時キルでガードが外れてしまっている場合
                        //自殺させる
                        target.RpcMurderPlayer(target);
                        //キルブロック解除
                        main.BlockKilling[__instance.PlayerId] = false;
                    }
                }, 0.2f, "Vampire Bite");
                main.BitPlayers.Add(target.PlayerId, (__instance.PlayerId, 0f));
                return false;
            }

            //==キル処理==
            __instance.RpcMurderPlayer(target);
            //============

            //BountyHunter
            if(__instance.isBountyHunter()) {
                if(target == __instance.getBountyTarget()) {
                    __instance.RpcGuardAndKill(target);
                    __instance.ResetBountyTarget();
                }
            }


            return false;
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
    class ReportDeadBodyPatch
    {
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
        {
            if (main.IsHideAndSeek) return false;
            if (!AmongUsClient.Instance.AmHost) return true;
            if (target != null)
            {
                Logger.info($"{__instance.name} => {target.PlayerName}","Report");
                if(main.IgnoreReportPlayers.Contains(target.PlayerId))
                {
                    Logger.info($"{target.PlayerName}は通報が禁止された死体なのでキャンセルされました");
                    return false;
                }
            }

            if (main.SyncButtonMode && target == null)
            {
                Logger.SendToFile("最大:" + main.SyncedButtonCount + ", 現在:" + main.UsedButtonCount, LogLevel.Message);
                if (main.SyncedButtonCount <= main.UsedButtonCount)
                {
                    Logger.SendToFile("使用可能ボタン回数が最大数を超えているため、ボタンはキャンセルされました。", LogLevel.Message);
                    return false;
                }
                else main.UsedButtonCount++;
                if (main.SyncedButtonCount == main.UsedButtonCount)
                {
                    Logger.SendToFile("使用可能ボタン回数が最大数に達しました。");
                }
            }

            foreach (var bp in main.BitPlayers)
            {
                var pc = main.getPlayerById(bp.Key);
                if(pc.Data.IsDead)
                    Logger.SendToFile("Vampireに噛まれている" + pc.name + "はすでに死んでいました。");
                else{
                    pc.RpcMurderPlayer(pc);
                    main.PlaySoundRPC(bp.Value.Item1, Sounds.KillSound);
                    Logger.SendToFile("Vampireに噛まれている" + pc.name + "を自爆させました。");
                }
            }
            main.BitPlayers = new Dictionary<byte, (byte, float)>();

            //if(__instance.Data.IsDead) return true;
            //=============================================
            //以下、ボタンが押されることが確定したものとする。
            //=============================================

            if(main.SyncButtonMode && AmongUsClient.Instance.AmHost && PlayerControl.LocalPlayer.Data.IsDead) {
                //SyncButtonMode中にホストが死んでいる場合
                ChangeLocalNameAndRevert(
                    "緊急会議ボタンはあと" + (main.SyncedButtonCount - main.UsedButtonCount) + "回使用可能です。",
                    1000
                );
            }
            
            main.CustomSyncAllSettings();
            return true;
        }
        public static async void ChangeLocalNameAndRevert(string name, int time) {
            //async Taskじゃ警告出るから仕方ないよね。
            var revertName = PlayerControl.LocalPlayer.name;
            PlayerControl.LocalPlayer.RpcSetName(name);
            await Task.Delay(time);
            PlayerControl.LocalPlayer.RpcSetName(revertName);
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (AmongUsClient.Instance.AmHost)
            {//実行クライアントがホストの場合のみ実行
                //Vampireの処理
                if (main.BitPlayers.ContainsKey(__instance.PlayerId))
                {
                    //__instance:キルされる予定のプレイヤー
                    //main.BitPlayers[__instance.PlayerId].Item1:キルしたプレイヤーのID
                    //main.BitPlayers[__instance.PlayerId].Item2:キルするまでの秒数
                    if (main.BitPlayers[__instance.PlayerId].Item2 >= main.VampireKillDelay)
                    {
                        byte vampireID = main.BitPlayers[__instance.PlayerId].Item1;
                        if (!__instance.Data.IsDead)
                        {
                            __instance.RpcMurderPlayer(__instance);
                            main.PlaySoundRPC(vampireID, Sounds.KillSound);
                            Logger.SendToFile("Vampireに噛まれている" + __instance.name + "を自爆させました。");
                        }
                        else
                            Logger.SendToFile("Vampireに噛まれている" + __instance.name + "はすでに死んでいました。");
                        main.BitPlayers.Remove(__instance.PlayerId);
                    }
                    else
                    {
                        main.BitPlayers[__instance.PlayerId] =
                        (main.BitPlayers[__instance.PlayerId].Item1, main.BitPlayers[__instance.PlayerId].Item2 + Time.fixedDeltaTime);
                    }
                }

                if(__instance.AmOwner) main.ApplySuffix();
                if(main.PluginVersionType == VersionTypes.Beta && AmongUsClient.Instance.IsGamePublic) AmongUsClient.Instance.ChangeGamePublic(false);
            }

            //役職テキストの表示
            var RoleTextTransform = __instance.nameText.transform.Find("RoleText");
            var RoleText = RoleTextTransform.GetComponent<TMPro.TextMeshPro>();
            if (RoleText != null && __instance != null)
            {
                var RoleTextData = main.GetRoleText(__instance);
                if(main.IsHideAndSeek) {
                    var hasRole = main.AllPlayerCustomRoles.TryGetValue(__instance.PlayerId, out var role);
                    if(hasRole) RoleTextData = main.GetRoleTextHideAndSeek(__instance.Data.Role.Role, role);
                }
                RoleText.text = RoleTextData.Item1;
                RoleText.color = RoleTextData.Item2;
                if (__instance.AmOwner) RoleText.enabled = true; //自分ならロールを表示
                else if (main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead) RoleText.enabled = true; //他プレイヤーでVisibleTasksCountが有効なおかつ自分が死んでいるならロールを表示
                else RoleText.enabled = false; //そうでなければロールを非表示
                if (!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.GameMode != GameModes.FreePlay)
                {
                    RoleText.enabled = false; //ゲームが始まっておらずフリープレイでなければロールを非表示
                    if(!__instance.AmOwner) __instance.nameText.text = __instance.name;
                }
                if (main.VisibleTasksCount && main.hasTasks(__instance.Data, false)) //他プレイヤーでVisibleTasksCountは有効なおかつタスクがあるなら
                    RoleText.text += $" <color=#e6b422>({main.getTaskText(__instance.Data.Tasks)})</color>"; //ロールの横にタスク表示
                

                //変数定義
                string RealName;
                string Mark = "";
                string Suffix = "";

                //名前変更
                RealName = __instance.getRealName();

                //名前色変更処理
                //自分自身の名前の色を変更
                if(__instance.AmOwner && AmongUsClient.Instance.IsGameStarted) { //__instanceが自分自身
                    RealName = $"<color={__instance.getRoleColorCode()}>{RealName}</color>"; //名前の色を変更
                }

                //タスクを終わらせたSnitchがインポスターを確認できる
                if(PlayerControl.LocalPlayer.isSnitch() && //LocalPlayerがSnitch
                    __instance.getCustomRole().isImpostor() && //__instanceがインポスター
                    PlayerControl.LocalPlayer.getPlayerTaskState().isTaskFinished //LocalPlayerのタスクが終わっている
                ) {
                    RealName = $"<color=#ff0000>{RealName}</color>"; //__instanceの名前を赤色で表示
                }
                else if(PlayerControl.LocalPlayer != null)
                {//NameColorManager準拠の処理
                    var ncd = NameColorManager.Instance.GetData(PlayerControl.LocalPlayer.PlayerId, __instance.PlayerId);
                    RealName = ncd.OpenTag + RealName + ncd.CloseTag;
                }

                //インポスターがタスクが終わりそうなSnitchを確認できる
                if(PlayerControl.LocalPlayer.getCustomRole().isImpostor() && //LocalPlayerがインポスター
                __instance.isSnitch() && __instance.getPlayerTaskState().doExpose //__instanceがタスクが終わりそうなSnitch
                ) {
                    Mark += $"<color={main.getRoleColorCode(CustomRoles.Snitch)}>★</color>"; //Snitch警告をつける
                }

                //タスクが終わりそうなSnitchがいるとき、インポスターに警告が表示される
                if(__instance.AmOwner && __instance.getCustomRole().isImpostor()) { //__instanceがインポスターかつ自分自身
                    foreach(var pc in PlayerControl.AllPlayerControls) { //全員分ループ
                        if(!pc.isSnitch() || pc.Data.IsDead || pc.Data.Disconnected) continue; //(スニッチ以外 || 死者 || 切断者)に用はない 
                        if(pc.getPlayerTaskState().doExpose) { //タスクが終わりそうなSnitchが見つかった時
                            Mark += $"<color={main.getRoleColorCode(CustomRoles.Snitch)}>★</color>"; //Snitch警告を表示
                            break; //無駄なループは行わない
                        }
                    }
                }

                /*if(main.AmDebugger.Value && main.BlockKilling.TryGetValue(__instance.PlayerId, out var isBlocked)) {
                    Mark = isBlocked ? "(true)" : "(false)";
                }*/

                //Mark・Suffixの適用
                __instance.nameText.text = $"{RealName}{Mark}";
                __instance.nameText.text += Suffix == "" ? "" : "\r\n" + Suffix;
            }
        }
    }
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
    class PlayerStartPatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            var roleText = UnityEngine.Object.Instantiate(__instance.nameText);
            roleText.transform.SetParent(__instance.nameText.transform);
            roleText.transform.localPosition = new Vector3(0f, 0.175f, 0f);
            roleText.fontSize = 0.55f;
            roleText.text = "RoleText";
            roleText.gameObject.name = "RoleText";
            roleText.enabled = false;
        }
    }
    [HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.SetColor))]
    class SetColorPatch {
        public static bool IsAntiGlitchDisabled = false;
        public static bool Prefix(PlayerControl __instance, int bodyColor) {
            //色変更バグ対策
            if(!AmongUsClient.Instance.AmHost || __instance.CurrentOutfit.ColorId == bodyColor || IsAntiGlitchDisabled) return true;
            if(AmongUsClient.Instance.IsGameStarted && main.IsHideAndSeek) {
                //ゲーム中に色を変えた場合
                __instance.RpcMurderPlayer(__instance);
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
    class EnterVentPatch {
        public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc) {
            if(main.IsHideAndSeek && main.IgnoreVent)
                pc.MyPhysics.RpcBootFromVent(__instance.Id);
        }
    }
    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
    class CoEnterVentPatch {
        public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id) {
            if(AmongUsClient.Instance.AmHost){
                if(__instance.myPlayer.isSheriff()) {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, -1);
                    writer.WritePacked(127);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                    new LateTask(() => {
                        int clientId = __instance.myPlayer.getClientId();
                        MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, clientId);
                        writer2.Write(id);
                        AmongUsClient.Instance.FinishRpcImmediately(writer2);
                    }, 0.5f, "Fix Sheriff Stuck");
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetName))]
    class SetNamePatch {
        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] string name) {
            main.RealNames[__instance.PlayerId] = name;
        }
    }
}
