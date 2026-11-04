using System.Windows;

namespace NHL27Kit.App;

public partial class App : Application
{
    /// <summary>
    /// Command line, for previews and scripted checks:
    ///   --shot FILE.png   render the window to FILE once it has settled, then exit
    ///   --no-index        do not load the asset index (the window shows its loading state)
    ///   --search TEXT     type TEXT into the search box
    ///   --kind ebx|res|chunk, --type TYPE, --folder PATH, --select N (select the Nth row, 0-based)
    ///   --page explorer|settings|…|scorebug, and for the Scorebug page --sbsharp (choose the sharp badge),
    ///   --sbsave (save into the game); for the Database page --dbname, --dbtable, --dbfind;
    ///   for the Roster page --rsync all|move|join|update|left|missing (fetch the NHL rosters and show that kind),
    ///   --rlines TEAMINDEX [--rlinespick "l1lw=McKenna;g1=Stolarz"] [--rlinesauto] [--rlinessave];
    ///   for the Uniforms page --ugeartab, --ugear "helmet:1=#FF0000;gloves:4=#00FF00", --ugearsave,
    ///   --uexport FILE --uslot SLOT (export a kit texture as PNG or DDS), --uslots (the Jersey slots tab),
    ///   --uslotadd "boston adidas home", --uslotmove "28:1;3:5" (menu place to place, 1-based), --uslotremove PLACE, --uslotname "PLACE=Name", --uslotsave;
    ///   --ulegacy FILE (convert a legacy-template jersey and preview it on the kit), --ulegacywrite [--ulegacykeeplogos];
    ///   for the Jersey Studio (--page studio): your own uniform --jcolors "#MAIN,#SECOND,#THIRD" --jfont N --jpant N --jsock N
    ///   --jletter "#FILL,#OUTLINE", or a team's kit --jselect N [--jsearch TEXT] [--jkit N]; then --jtemplate adidas:5|kit, --jimage FILE [--jlegacy],
    ///   --jcolor "z0=#FF0000;r4=#FFFFFF", --jline "body:10,5,#FFFFFF,#C8102E,1;sleeves:6,4,#FFFFFF", --jlogo "FILE|0.5,0.2,0.1",
    ///   --jspot "8:0.5,0.27,1.2", --jslot "TEAM|kit folder", --jtemplatefile FILE.dds|png, --jturn DEG, --jzoom F [--jup U], --jscroll Y, --jwrite, --jaddteam;
    ///   for the Teams page --tsearch TEXT, --tteam ABBR|INDEX|NAME, --tset "feprimarycolor=#FF0000;arena.arenaname=X",
    ///   --tsave, --tscroll OFFSET, --tlogoexport DIR, --tlogo main|all|STYLE --tlogofile IMAGE;
    ///   for the Tuning page --tusearch TEXT, --tugroup PATH, --turatings, --tucsv FILE, --tumatch (NHL 24: Match NHL 27, no dialogs);
    ///   for the Be A Pro page --bapdir SAVES (read another save folder), --bapslot NAME, --bapset "Overall=88;xf2=4:3" (not saved),
    ///   --bapadvanced, --bapscroll Y, --bapsave;
    ///   for the Arenas page --arena LEVEL|NAME, --aview broadcast|end|bowl|overhead|ice, --alook "YAW,PITCH,DIST[,HEIGHT]",
    ///   --anoglass, --anoice, --anoroof, --anoprops, --anojumbo, --agameads (the game's centre-ice ads, not the team's);
    ///   for NHL 24's Play page --p24pc DIR (another NHL24 PC folder), --p24set "GPU.vblank_frequency=120;GPU.window_*=1920x1080", --p24save, --p24scroll Y;
    ///   for the Videos page --vsearch TEXT, --vcat KIND, --vselect N [--vwait MS] [--vmp4 FILE]
    /// </summary>
    public static Dictionary<string, string> Args { get; } = new(StringComparer.OrdinalIgnoreCase);

    protected override void OnStartup(StartupEventArgs e)
    {
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (!e.Args[i].StartsWith("--"))
                continue;
            string key = e.Args[i][2..];
            Args[key] = i + 1 < e.Args.Length && !e.Args[i + 1].StartsWith("--") ? e.Args[++i] : "";
        }
        SoundPlayerView.CleanTemp();
        base.OnStartup(e);
    }
}
