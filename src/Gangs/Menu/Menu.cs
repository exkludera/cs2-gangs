using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace Gangs;

public static partial class Menu
{
    private static Plugin Instance = Plugin.Instance;

    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public static void Command_OpenMenus(CCSPlayerController player, CommandInfo info)
    {
        if (Instance._api!.OnlyTerroristCheck(player))
        {
            Instance.PrintToChat(player, Instance.Localizer["chat<only_terrorists>"]);
            return;
        }

        switch (Instance.Config.Settings.MenuType.ToLower())
        {
            case "chat":
            case "text":
                MenuChat.Open(player);
                break;
            case "html":
            case "center":
            case "centerhtml":
            case "hud":
                MenuHTML.Open(player);
                break;
            default:
                MenuChat.Open(player);
                break;
        }
    }
}