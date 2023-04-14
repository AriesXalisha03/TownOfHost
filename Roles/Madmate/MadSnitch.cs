using System.Linq;

using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Madmate;

public sealed class MadSnitch : RoleBase, IKillFlashSeeable
{
    public static SimpleRoleInfo RoleInfo =
        new(
            typeof(MadSnitch),
            player => new MadSnitch(player),
            CustomRoles.MadSnitch,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            10200,
            SetupOptionItem
        );
    public MadSnitch(PlayerControl player)
    : base(
        RoleInfo,
        player,
        HasTask.ForRecompute)
    {
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();

        canVent = OptionCanVent.GetBool();
        canAlsoBeExposedToImpostor = OptionCanAlsoBeExposedToImpostor.GetBool();

        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    private static OptionItem OptionCanVent;
    private static OptionItem OptionCanAlsoBeExposedToImpostor;
    private static Options.OverrideTasksData Tasks;
    enum OptionName
    {
        CanVent,
        MadSnitchCanAlsoBeExposedToImpostor,
    }

    public static bool canSeeKillFlash;
    private static bool canVent;
    private static bool canAlsoBeExposedToImpostor;

    public static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 10, OptionName.CanVent, false, false);
        OptionCanAlsoBeExposedToImpostor = BooleanOptionItem.Create(RoleInfo, 11, OptionName.MadSnitchCanAlsoBeExposedToImpostor, false, false);
        Tasks = Options.OverrideTasksData.Create(RoleInfo, 20);
    }

    public bool KnowsImpostor()
    {
        return Player.GetPlayerTaskState().IsTaskFinished;
    }

    public override bool OnCompleteTask()
    {
        if (KnowsImpostor())
        {
            foreach (var impostor in Main.AllPlayerControls.Where(player => player.Is(CustomRoleTypes.Impostor)).ToArray())
            {
                NameColorManager.Add(Player.PlayerId, impostor.PlayerId, impostor.GetRoleColorCode());
            }
        }

        return true;
    }
    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        if (
            // オプションが無効
            !canAlsoBeExposedToImpostor ||
            // インポスター→MadSnitchではない
            !seer.Is(CustomRoleTypes.Impostor) ||
            (seen ??= seer).GetRoleClass() is not MadSnitch madSnitch ||
            // マッドスニッチがまだインポスターを知らない
            !madSnitch.KnowsImpostor())
        {
            return string.Empty;
        }

        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.MadSnitch), "★");
    }

    public bool CanSeeKillFlash(MurderInfo info) => canSeeKillFlash;
}
