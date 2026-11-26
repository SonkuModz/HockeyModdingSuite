package
{
   import flash.display.DisplayObject;
   import flash.display.GradientType;
   import flash.display.Graphics;
   import flash.display.Loader;
   import flash.display.MovieClip;
   import flash.display.Shape;
   import flash.display.Sprite;
   import flash.events.Event;
   import flash.filters.DropShadowFilter;
   import flash.filters.GlowFilter;
   import flash.geom.Matrix;
   import flash.geom.Point;
   import flash.geom.Rectangle;
   import flash.net.URLRequest;
   import flash.text.AntiAliasType;
   import flash.text.TextField;
   import flash.text.TextFieldAutoSize;
   import flash.text.TextFormat;
   import flash.utils.getDefinitionByName;
   import flash.utils.getTimer;

   // IHN (Ice Hockey Network) score clock for NHL 24, design "R1 Gunmetal": a bar at the top left of the screen.
   // Away wing | raised clock | home wing. No shots row: the power play box drops into that space under the team that
   // has it; the middle box shows only for popups / neutral warnings / the shootout (round markers in the team boxes).
   // Same public API as the NHL24Kit ESPN+ / HMS Sportsnet clocks, so the game drives it the same way.
   public class ScoreClock extends MovieClip
   {
      private static var X0:Number;
      private static var Y0:Number;
      private static var SCALE:Number;

      // layout, in design pixels (1920x1080); the bar's origin is its top-left; the anchor (x, y) is the plate's top-left corner
      private static const BW:Number = 640;
      private static const WING:Number = 262;
      private static const SLANT:Number = 38;
      private static const CX:Number = 246;
      private static const CW:Number = 148;
      private static const RAISE:Number = 10;
      private static const TOP:Number = 70;
      private static const ROW_Y:Number = 72;
      private static const ROW_H:Number = 30;
      private static const TAB_Y:Number = 104;
      private static const TAB_H:Number = 28;
      private static const LOGO:Number = 60;
      private static const GOAL_LOGO:Number = 128;
      private static const GOAL_H:Number = 102;

      private static var C_FRAME1:uint;
      private static var C_FRAME2:uint;
      private static var C_BOX1:uint;
      private static var C_BOX2:uint;
      private static var C_RIM:uint;
      private static var C_CLOCK:uint;
      private static var C_MARK:uint;
      private static var C_TEXT:uint;
      private static var C_PENALTY:uint;
      private static var C_MISS:uint;

      private static var S_ABBR:Number;
      private static var S_SCORE:Number;
      private static var S_CLOCK:Number;
      private static var S_PERIOD:Number;
      private static var S_ROW:Number;
      private static var S_TAB:Number;
      private static var S_SIDE:Number;
      private static var S_GOAL:Number;
      private static var S_NAME:Number;

      private static var GLOW:Number;
      private static var STREAKS:Boolean;
      private static var SHOTS_ROW:Boolean;
      private static var GOAL_STYLE:int;

      private static var L_PP:String;
      private static var L_GOAL:String;
      private static var L_SHOOTOUT:String;
      private static var L_ROUND:String;
      private static var L_SHOTS:String;
      private static var L_FINAL:String;
      private static var L_MID:String;
      private static var L_PENALTY:String;
      private static var L_PERIOD:String;
      private static var L_OT:String;
      private static var L_MARK:String;

      private static var GOAL_MIN_HOLD:Number;
      private static var GOAL_MAX_HOLD:Number;
      private static var SIDE_HOLD:Number;
      private static var SHOTS_HOLD:Number;

      private static var TEAM_COLOURS:Object;
      private static var TEAM_NAMES:Object;

      private static const HOME:Number = 0;
      private static const AWAY:Number = 1;
      private static const NEUTRAL:Number = 2;
      private static const FONT:String = "Stratum1-Bold";
      private static const FONT_HEAVY:String = "NN Rektorat STD Heavy";
      private static const AUDIO_GOAL_AWAY:String = "scoreclock_goalscored_show_away";
      private static const AUDIO_GOAL_HOME:String = "scoreclock_goalscored_show_home";
      private static const AUDIO_PENALTY_AWAY:String = "scoreclock_penalty_show_away";
      private static const AUDIO_PENALTY_HOME:String = "scoreclock_penalty_show_home";
      private static const AUDIO_HIGHLIGHT_AWAY:String = "scoreclock_player_highlight_show_away";
      private static const AUDIO_HIGHLIGHT_HOME:String = "scoreclock_player_highlight_show_home";
      private static const TIMED_GOAL_HOLD:Number = 3;
      private static const DEFAULT_NAMES:String = "ANA:DUCKS|ARI:COYOTES|BOS:BRUINS|BUF:SABRES|CGY:FLAMES|CAR:HURRICANES|CHI:BLACKHAWKS|COL:AVALANCHE|CBJ:BLUE JACKETS|DAL:STARS|DET:RED WINGS|EDM:OILERS|FLA:PANTHERS|LA:KINGS|MIN:WILD|MTL:CANADIENS|NSH:PREDATORS|NJ:DEVILS|NYI:ISLANDERS|NYR:RANGERS|OTT:SENATORS|PHI:FLYERS|PIT:PENGUINS|SJ:SHARKS|SEA:KRAKEN|STL:BLUES|TBL:LIGHTNING|TOR:MAPLE LEAFS|VAN:CANUCKS|VGK:GOLDEN KNIGHTS|WSH:CAPITALS|WPG:JETS";
__CFG__
      private static var _configured:Boolean = false;

      private var _anchor:Sprite;
      private var _zoom:Number = 1;
      private var _bug:Sprite;
      private var _main:Sprite;
      private var _frame:Shape;
      private var _wings:Array = [];
      private var _wingArt:Array = [];
      private var _accents:Array = [];
      private var _logo:Array = [];
      private var _abbr:Array = [];
      private var _score:Array = [];
      private var _flash:Array = [];
      private var _center:Sprite;
      private var _periodText:TextField;
      private var _clockText:TextField;
      private var _practiceText:TextField;
      private var _markText:TextField;
      private var _row:Sprite;
      private var _rowBox:Array = [];
      private var _midShown:Boolean = false;
      private var _rowLabel:Array = [];
      private var _rowVal:Array = [];
      private var _midBox:Shape;
      private var _midText:TextField;
      private var _midTime:TextField;
      private var _dots:Shape;
      private var _tab:Sprite;
      private var _tabBg:Shape;
      private var _tabLabel:TextField;
      private var _tabTime:TextField;
      private var _tabOpen:Boolean = false;
      private var _tabTeam:int = -1;
      private var _side:Sprite;
      private var _sideBg:Shape;
      private var _sideText:TextField;
      private var _sideText2:TextField;
      private var _sideOpen:Boolean = false;
      private var _sideGen:int = 0;
      private var _shotsGen:int = 0;
      private var _goalGen:int = 0;
      private var _goal:Sprite;
      private var _goalArt:Sprite;
      private var _goalBg:Shape;
      private var _goalMask:Shape;
      private var _goalSweep:Shape;
      private var _goalLogoBox:Sprite;
      private var _goalLogoHolder:Array = [];
      private var _goalName:TextField;
      private var _goalLetters:Array = [];
      private var _letterX:Array = [];
      private var _goalWord:Sprite;
      private var _goalRing:Shape;
      private var _goalStrobe:Shape;
      private var _goalLine:Shape;
      private var _wordLeft:Number = 0;
      private var _wordW:Number = 0;
      private var _goalCount:int = 0;
      private var _style:int = 1;
      private var _goalIdx:int = 0;
      private var _logoRestX:Number = 0;
      private var _fits:Array = [];
      private static const LETTER_Y:Number = 47;
      private var _goalPP:TextField;
      private var _goalUp:Boolean = false;
      private var _goalPending:Boolean = false;
      private var _goalStart:int = 0;
      private var _goalIsPP:Boolean = false;
      private var _covered:Boolean = false;
      private var _pendingPulse:Array = [false,false];
      private var _pendingSide:Array = null;
      private var _primary:Array = [16009784,13111342];
      private var _secondary:Array = [16009784,13111342];
      private var _names:Array = ["AWAY","HOME"];
      private var _scores:Array = ["0","0"];
      private var _shots:Array = ["0","0"];
      private var _lastShots:Array = ["0","0"];
      private var _rowName:String = null;
      private var _final:Boolean = false;
      private var _period:String = "";
      private var _warnSide:Number = -1;
      private var _warnText:String = null;
      private var _warnTime:String = null;
      private var _popupText:String = null;
      private var _goalSide:Number = -1;
      private var _shootout:Object = null;
      private var _isShootout:Boolean = false;
      private var _visible:Boolean = false;
      private var _audioCallBack:Function;
      private var _signalCallBack:Function;
      private var _isDebug:Boolean = false;
      private var _tweens:Array = [];
      private var _state:Number = 0;
      private var _instant:Boolean = false;

      public function ScoreClock()
      {
         super();
         if(!_configured)
         {
            loadConfig(CFG);
         }
         _bug = new Sprite();
         _bug.alpha = 0;
         _bug.visible = false;
         _anchor = new Sprite();
         addChild(_anchor);
         _anchor.addChild(_bug);
         pin();
         build();
         addEventListener(Event.ENTER_FRAME,onFrame);
         addEventListener(Event.ADDED_TO_STAGE,onAdded);
      }

      // ---------------------------------------------------------------- settings

      private static function cfgNum(c:Object, k:String, d:Number) : Number
      {
         if(c[k] == null || c[k] == "")
         {
            return d;
         }
         var v:Number = Number(c[k]);
         return isNaN(v) ? d : v;
      }

      private static function cfgStr(c:Object, k:String, d:String) : String
      {
         return c[k] == null || c[k] == "" ? d : String(c[k]);
      }

      private static function cfgFlag(c:Object, k:String, d:Boolean) : Boolean
      {
         return c[k] == null || c[k] == "" ? d : c[k] == "1" || c[k] == "true";
      }

      private static function readPairs(s:String, upper:Boolean) : Object
      {
         var o:Object = {};
         var parts:Array = s.split("|");
         var i:int = 0;
         while(i < parts.length)
         {
            var f:Array = String(parts[i]).split(":");
            if(f.length >= 2 && trim(String(f[0])) != "")
            {
               o[trim(String(f[0])).toUpperCase()] = upper ? trim(String(f[1])).toUpperCase() : trim(String(f[1]));
            }
            i++;
         }
         return o;
      }

      private static function loadConfig(s:String) : void
      {
         var c:Object = {};
         if(s != null)
         {
            if(s.indexOf("#NHL24KIT_CFG#") == 0)
            {
               s = s.substr(14);
            }
            var parts:Array = s.split(";");
            var i:int = 0;
            while(i < parts.length)
            {
               var part:String = String(parts[i]);
               var eq:int = part.indexOf("=");
               if(eq > 0)
               {
                  c[trim(part.substr(0,eq))] = trim(part.substr(eq + 1));
               }
               i++;
            }
         }
         X0 = cfgNum(c,"x",36);
         Y0 = cfgNum(c,"y",30);
         SCALE = Math.max(0.3,Math.min(3,cfgNum(c,"scale",0.92)));
         C_FRAME1 = cfgNum(c,"cFrame",2764341);
         C_FRAME2 = cfgNum(c,"cFrameDark",921620);
         C_BOX1 = cfgNum(c,"cBox",2304814);
         C_BOX2 = cfgNum(c,"cBoxDark",724240);
         C_RIM = cfgNum(c,"cRim",9146779);
         C_CLOCK = cfgNum(c,"cClock",15902778);
         C_MARK = cfgNum(c,"cMark",13215052);
         C_TEXT = cfgNum(c,"cText",16777215);
         C_PENALTY = cfgNum(c,"cPenalty",16730698);
         C_MISS = cfgNum(c,"cMiss",10134445);
         S_ABBR = cfgNum(c,"sAbbr",38);
         S_SCORE = cfgNum(c,"sScore",42);
         S_CLOCK = cfgNum(c,"sClock",34);
         S_PERIOD = cfgNum(c,"sPeriod",16);
         S_ROW = cfgNum(c,"sRow",17);
         S_TAB = cfgNum(c,"sTab",17);
         S_SIDE = cfgNum(c,"sSide",19);
         S_GOAL = cfgNum(c,"sGoal",62);
         S_NAME = cfgNum(c,"sName",15);
         GLOW = Math.max(0,Math.min(1,cfgNum(c,"glow",0.8)));
         STREAKS = cfgFlag(c,"streaks",true);
         SHOTS_ROW = cfgFlag(c,"shotsRow",true);
         GOAL_STYLE = int(Math.max(0,Math.min(4,cfgNum(c,"goalStyle",0))));
         L_PP = cfgStr(c,"lPowerPlay","POWER PLAY");
         L_GOAL = cfgStr(c,"lGoal","GOAL");
         L_SHOOTOUT = cfgStr(c,"lShootout","SHOOTOUT");
         L_ROUND = cfgStr(c,"lRound","RD");
         L_SHOTS = cfgStr(c,"lShots","SOG");
         L_FINAL = cfgStr(c,"lFinal","FINAL");
         L_MID = cfgStr(c,"lMid","IHN HOCKEY");
         L_PENALTY = cfgStr(c,"lPenalty","PENALTY");
         L_PERIOD = cfgStr(c,"lPeriod","PERIOD");
         L_OT = cfgStr(c,"lOvertime","OVERTIME");
         L_MARK = cfgStr(c,"lMark","IHN");
         GOAL_MIN_HOLD = cfgNum(c,"goalMinHold",4.4);
         GOAL_MAX_HOLD = Math.max(GOAL_MIN_HOLD + 0.5,cfgNum(c,"goalMaxHold",6.5));
         SIDE_HOLD = cfgNum(c,"sideHold",5);
         SHOTS_HOLD = cfgNum(c,"shotsHold",4);
         TEAM_COLOURS = {};
         var tc:Array = cfgStr(c,"teamColors","").split("|");
         i = 0;
         while(i < tc.length)
         {
            var f:Array = String(tc[i]).split(":");
            if(f.length >= 2 && trim(String(f[0])) != "")
            {
               var pri:Number = Number("0x" + trim(String(f[1])));
               var sec:Number = f.length >= 3 ? Number("0x" + trim(String(f[2]))) : pri;
               if(!isNaN(pri))
               {
                  TEAM_COLOURS[trim(String(f[0])).toUpperCase()] = [uint(pri),uint(isNaN(sec) ? pri : sec)];
               }
            }
            i++;
         }
         TEAM_NAMES = readPairs(DEFAULT_NAMES,true);
         var extra:Object = readPairs(cfgStr(c,"teamNames",""),true);
         for(var k:String in extra)
         {
            TEAM_NAMES[k] = extra[k];
         }
         _configured = true;
      }

      // ---------------------------------------------------------------- helpers

      private static function trim(s:String) : String
      {
         var a:int = 0;
         var b:int = s.length;
         while(a < b && s.charCodeAt(a) <= 32)
         {
            a++;
         }
         while(b > a && s.charCodeAt(b - 1) <= 32)
         {
            b--;
         }
         return s.substring(a,b);
      }

      private static function allDigits(s:String) : Boolean
      {
         var i:int = 0;
         while(i < s.length)
         {
            var c:Number = s.charCodeAt(i);
            if(c < 48 || c > 57)
            {
               return false;
            }
            i++;
         }
         return s.length > 0;
      }

      private static function isClock(s:String) : Boolean
      {
         var colon:int = s.indexOf(":");
         if(colon < 0)
         {
            var dot0:int = s.indexOf(".");
            return dot0 > 0 && dot0 == s.length - 2 && allDigits(s.substr(0,dot0)) && allDigits(s.substr(dot0 + 1));
         }
         if(colon < 1 || colon > 2)
         {
            return false;
         }
         var rest:String = s.substr(colon + 1);
         var dot:int = rest.indexOf(".");
         var sec:String = dot >= 0 ? rest.substr(0,dot) : rest;
         if(dot >= 0 && (rest.length - dot - 1 != 1 || !allDigits(rest.substr(dot + 1))))
         {
            return false;
         }
         return allDigits(s.substr(0,colon)) && sec.length == 2 && allDigits(sec);
      }

      private static function splitClock(s:String) : Array
      {
         s = trim(s);
         var sp:int = s.lastIndexOf(" ");
         var last:String = sp >= 0 ? s.substr(sp + 1) : s;
         if(isClock(last))
         {
            return [sp >= 0 ? trim(s.substr(0,sp)) : "",last];
         }
         return [s,null];
      }

      private static function lum(c:uint) : Number
      {
         return (0.299 * (c >> 16 & 0xFF) + 0.587 * (c >> 8 & 0xFF) + 0.114 * (c & 0xFF)) / 255;
      }

      private static function mix(a:uint, b:uint, t:Number) : uint
      {
         var r:uint = (a >> 16 & 0xFF) * (1 - t) + (b >> 16 & 0xFF) * t;
         var g:uint = (a >> 8 & 0xFF) * (1 - t) + (b >> 8 & 0xFF) * t;
         var bl:uint = (a & 0xFF) * (1 - t) + (b & 0xFF) * t;
         return r << 16 | g << 8 | bl;
      }

      // a bright version of a team colour, for glows and streaks (dark navies and blacks would vanish on gunmetal)
      private static function bright(c:uint) : uint
      {
         var l:Number = lum(c);
         if(l < 0.12)
         {
            return mix(c,16777215,0.45);
         }
         if(l < 0.3)
         {
            return mix(c,16777215,0.32);
         }
         return mix(c,16777215,0.12);
      }

      private static function setFieldText(t:TextField, s:String, color:uint) : void
      {
         var f:TextFormat = t.defaultTextFormat;
         f.color = color;
         t.defaultTextFormat = f;
         t.text = s == null || s == "" ? " " : s;
         t.setTextFormat(f);
      }

      private static function spacing(t:TextField, px:Number) : void
      {
         var f:TextFormat = t.defaultTextFormat;
         f.letterSpacing = px;
         t.defaultTextFormat = f;
         t.setTextFormat(f);
      }

      private static function fit(t:TextField, w:Number) : void
      {
         t.scaleX = t.scaleY = 1;
         if(t.width > w && t.width > 0)
         {
            t.scaleX = t.scaleY = w / t.width;
         }
      }

      private static function centerY(t:TextField, top:Number, h:Number) : void
      {
         t.y = Math.round(top + (h - t.height) / 2 + t.height * 0.02);
      }

      private static function poly(g:Graphics, pts:Array) : void
      {
         g.moveTo(pts[0],pts[1]);
         var k:int = 2;
         while(k < pts.length)
         {
            g.lineTo(pts[k],pts[k + 1]);
            k += 2;
         }
         g.lineTo(pts[0],pts[1]);
      }

      // a dark box with a metal rim: the frame look of every part
      private static function metalBox(g:Graphics, pts:Array, y:Number, h:Number, c1:uint, c2:uint, rim:uint, rimA:Number) : void
      {
         var m:Matrix = new Matrix();
         m.createGradientBox(BW,h,Math.PI / 2,0,y);
         g.lineStyle(1.5,rim,rimA,true);
         g.beginGradientFill(GradientType.LINEAR,[c1,c2],[1,1],[0,255],m);
         poly(g,pts);
         g.endFill();
         g.lineStyle();
      }

      private static function wingPts(i:int) : Array
      {
         return i == 0 ? [SLANT,0,WING,0,WING,TOP,0,TOP] : [BW - WING,0,BW - SLANT,0,BW,TOP,BW - WING,TOP];
      }

      private static function rowPts(i:int) : Array
      {
         if(i == 0)
         {
            return [38,ROW_Y,CX - 2,ROW_Y,CX - 2,ROW_Y + ROW_H,52,ROW_Y + ROW_H];
         }
         if(i == 1)
         {
            return [BW - CX + 2,ROW_Y,BW - 38,ROW_Y,BW - 52,ROW_Y + ROW_H,BW - CX + 2,ROW_Y + ROW_H];
         }
         return [CX + 4,ROW_Y,CX + CW - 4,ROW_Y,CX + CW - 14,ROW_Y + ROW_H,CX + 14,ROW_Y + ROW_H];
      }

      private function teamPri(i:int) : uint
      {
         var o:Array = TEAM_COLOURS[String(_names[i]).toUpperCase()];
         var c:uint = o != null ? uint(o[0]) : uint(_primary[i]);
         var s:uint = o != null ? uint(o[1]) : uint(_secondary[i]);
         // black primaries (PIT, LA, VGK...) glow in their secondary colour instead
         return lum(c) < 0.06 && lum(s) > 0.12 ? s : c;
      }

      private function teamGlow(i:int) : uint
      {
         return bright(teamPri(i));
      }

      private function nickname(i:int) : String
      {
         var n:String = String(_names[i]).toUpperCase();
         var o:* = TEAM_NAMES[n];
         return o != null ? String(o) : n;
      }

      private function sideIndex(side:Number) : int
      {
         return side == AWAY ? 0 : (side == HOME ? 1 : -1);
      }

      private function text(font:String, size:Number, color:uint) : TextField
      {
         var t:TextField = new TextField();
         t.embedFonts = true;
         t.selectable = false;
         t.mouseEnabled = false;
         t.antiAliasType = AntiAliasType.ADVANCED;
         t.autoSize = TextFieldAutoSize.LEFT;
         t.multiline = false;
         t.wordWrap = false;
         var f:TextFormat = new TextFormat(font,size,color);
         f.letterSpacing = 0;
         t.defaultTextFormat = f;
         t.text = " ";
         return t;
      }

      private function logoHolder(size:Number) : Sprite
      {
         var h:Sprite = new Sprite();
         _fits.push({"h":h,"s":size,"k":""});
         return h;
      }

      // scale the loaded logo to fill the box, and centre it both ways
      private function fitLogo(o:Object) : void
      {
         var h:Sprite = o.h;
         if(h.numChildren == 0)
         {
            return;
         }
         var c:DisplayObject = h.getChildAt(0);
         var b:Rectangle = c.getBounds(c);
         if(b.width < 16 || b.height < 16)
         {
            return;
         }
         // bounds only: reading the pixels of the game's logo textures isn't safe
         var key:String = b.x + "," + b.y + "," + b.width + "," + b.height;
         if(key == o.k && c == o.w)
         {
            return;
         }
         o.k = key;
         o.w = c;
         var r:Rectangle = b;
         var size:Number = Number(o.s);
         var k:Number = size / Math.max(r.width,r.height);
         c.scaleX = c.scaleY = k;
         c.x = (size - r.width * k) / 2 - r.x * k;
         c.y = (size - r.height * k) / 2 - r.y * k;
      }
      // ---------------------------------------------------------------- building

      private function rebuild() : void
      {
         _tweens = [];
         _anchor.removeChild(_bug);
         _bug = new Sprite();
         _bug.alpha = 0;
         _bug.visible = false;
         _anchor.addChild(_bug);
         _wings = [];
         _wingArt = [];
         _accents = [];
         _logo = [];
         _abbr = [];
         _score = [];
         _flash = [];
         _rowBox = [];
         _rowLabel = [];
         _rowVal = [];
         _goalLogoHolder = [];
         _goalLetters = [];
         _letterX = [];
         _fits = [];
         _tabOpen = _sideOpen = _goalUp = _goalPending = _covered = false;
         build();
         pin();
      }

      private function onAdded(e:Event) : void
      {
         removeEventListener(Event.ADDED_TO_STAGE,onAdded);
         var p:Object = loaderInfo != null ? loaderInfo.parameters : null;
         if(p == null || p.nhl24kitPreview != "1")
         {
            return;
         }
         _instant = p.instant == "1";
         if(p.goalStyle != null)
         {
            GOAL_STYLE = int(p.goalStyle);
         }
         if(p.cfg != null && p.cfg != "")
         {
            loadConfig(String(p.cfg));
            rebuild();
         }
         if(p.zoom != null)
         {
            _zoom = Number(p.zoom);
            pin();
         }
         init({
            "teamColorAway":uint(p.awayColor || "0xFC4C02"),
            "teamSecondaryColorAway":uint(p.awaySecondary || p.awayColor || "0x041E42"),
            "teamColorHome":uint(p.homeColor || "0xC8102E"),
            "teamSecondaryColorHome":uint(p.homeSecondary || p.homeColor || "0xF1BE48"),
            "logoPathAway":p.awayLogo,
            "logoPathHome":p.homeLogo
         },null,null);
         setAwayTeamName(p.awayName != null ? p.awayName : "EDM");
         setHomeTeamName(p.homeName != null ? p.homeName : "CGY");
         setScores([p.awayScore != null ? p.awayScore : "2",p.homeScore != null ? p.homeScore : "1"],false);
         setPeriod(p.period != null ? p.period : "1ST");
         setTime(p.clock != null ? p.clock : "07:54");
         showClock(true);
         showComparison([p.awayShots || "12",p.homeShots || "24","SHOTS"]);
         for each(var st:String in (String(p.state || "")).split(","))
         {
            if(st == "pp")
            {
               showWarning(HOME,"POWER PLAY " + (p.ppTime || "1:36"),false,true);
            }
            if(st == "ppaway")
            {
               showWarning(AWAY,"POWER PLAY " + (p.ppTime || "1:36"),false,true);
            }
            if(st == "pptick")
            {
               updatePowerplayTime(AWAY,"POWER PLAY " + (p.ppTime || "1:52"));
            }
            if(st == "en")
            {
               showWarning(HOME,"EMPTY NET",false,true);
            }
            if(st == "info")
            {
               showDropdown(HOME,p.info || "10 SAVES TONIGHT",null);
            }
            if(st == "goal")
            {
               showGoalWithDelay(AWAY,0.5);
            }
            if(st == "goalnow")
            {
               showGoal(AWAY);
            }
            if(st == "goalhome")
            {
               showGoalWithDelay(HOME,0.5);
            }
            if(st == "goalhomenow")
            {
               showGoal(HOME);
            }
            if(st == "popup")
            {
               showPopup(p.popup || "4-ON-4 1:10");
            }
            if(st == "final")
            {
               setPeriod("FINAL");
               setTime("");
            }
            if(st == "so")
            {
               updateShootoutData({
                  "numRounds":3,
                  "awayRound1":2,
                  "awayRound2":1,
                  "homeRound1":1,
                  "homeRound2":2
               });
               showShootout();
            }
         }
      }

      private function build() : void
      {
         _main = new Sprite();
         _main.x = 14;
         _main.y = RAISE;
         _bug.addChild(_main);
         _side = new Sprite();
         _sideBg = new Shape();
         _side.addChild(_sideBg);
         _sideText = text(FONT,S_SIDE,C_TEXT);
         _sideText2 = text(FONT,S_SIDE * 0.82,C_TEXT);
         _side.addChild(_sideText);
         _side.addChild(_sideText2);
         _side.visible = false;
         _side.alpha = 0;
         _main.addChild(_side);
         _tab = new Sprite();
         _tabBg = new Shape();
         _tab.addChild(_tabBg);
         _tabLabel = text(FONT,S_TAB,C_CLOCK);
         _tabTime = text(FONT,S_TAB,C_TEXT);
         spacing(_tabLabel,1);
         _tab.addChild(_tabLabel);
         _tab.addChild(_tabTime);
         _tab.visible = false;
         _tab.alpha = 0;
         _main.addChild(_tab);
         buildRow();
         _frame = new Shape();
         var fg:Graphics = _frame.graphics;
         metalBox(fg,[SLANT - 6,-4,BW - SLANT + 6,-4,BW + 12,TOP + 4,-12,TOP + 4],-4,TOP + 8,C_BOX1,0,C_RIM,0.9);
         _main.addChild(_frame);
         var i:int = 0;
         while(i < 2)
         {
            buildWing(i);
            i++;
         }
         buildCenter();
         buildGoal();
         repaint();
      }

      private function buildWing(i:int) : void
      {
         var acc:Shape = new Shape();
         _main.addChild(acc);
         _accents[i] = acc;
         var w:Sprite = new Sprite();
         var art:Shape = new Shape();
         w.addChild(art);
         var mask:Shape = new Shape();
         mask.graphics.beginFill(0);
         poly(mask.graphics,wingPts(i));
         mask.graphics.endFill();
         w.addChild(mask);
         art.mask = mask;
         var lh:Sprite = logoHolder(LOGO);
         lh.x = i == 0 ? 44 : BW - 44 - LOGO;
         lh.y = Math.round((TOP - LOGO) / 2);
         w.addChild(lh);
         var ab:TextField = text(FONT,S_ABBR,C_TEXT);
         ab.filters = [new DropShadowFilter(2,90,0,0.7,3,3,1)];
         w.addChild(ab);
         var sc:TextField = text(FONT_HEAVY,S_SCORE,C_TEXT);
         sc.filters = [new DropShadowFilter(0,90,0,0.6,8,8,1)];
         w.addChild(sc);
         var fl:Shape = new Shape();
         fl.graphics.beginFill(16777215);
         poly(fl.graphics,wingPts(i));
         fl.graphics.endFill();
         fl.alpha = 0;
         w.addChild(fl);
         _main.addChild(w);
         _wings[i] = w;
         _wingArt[i] = art;
         _logo[i] = lh;
         _abbr[i] = ab;
         _score[i] = sc;
         _flash[i] = fl;
      }

      private function buildCenter() : void
      {
         _center = new Sprite();
         var g:Graphics = _center.graphics;
         var pts:Array = [CX,-RAISE,CX + CW,-RAISE,CX + CW - 16,TOP,CX + 16,TOP];
         var m:Matrix = new Matrix();
         m.createGradientBox(CW,TOP + RAISE,Math.PI / 2,CX,-RAISE);
         g.lineStyle(2,C_RIM,1,true);
         g.beginGradientFill(GradientType.LINEAR,[mix(C_BOX1,16777215,0.04),C_BOX2],[1,1],[0,255],m);
         poly(g,pts);
         g.endFill();
         g.lineStyle();
         var fm:Matrix = new Matrix();
         fm.createGradientBox(CW - 40,3,0,CX + 20,-RAISE + 4);
         g.beginGradientFill(GradientType.LINEAR,[C_CLOCK,C_CLOCK,C_CLOCK],[0,1,0],[0,128,255],fm);
         g.drawRect(CX + 20,-RAISE + 4,CW - 40,2);
         g.endFill();
         _periodText = text(FONT,S_PERIOD,C_TEXT);
         _clockText = text(FONT,S_CLOCK,C_CLOCK);
         _clockText.filters = [new GlowFilter(C_CLOCK,0.35,8,8,1,1)];
         _practiceText = text(FONT,18,C_TEXT);
         _practiceText.visible = false;
         _markText = text(FONT,10,C_MARK);
         spacing(_markText,3);
         setFieldText(_markText,L_MARK,C_MARK);
         _markText.x = Math.round(CX + (CW - _markText.width) / 2);
         _markText.y = TOP - _markText.height + 1;
         _center.addChild(_periodText);
         _center.addChild(_clockText);
         _center.addChild(_practiceText);
         _center.addChild(_markText);
         _main.addChild(_center);
         setTime("20:00");
         setPeriod("1ST");
      }

      private function buildRow() : void
      {
         _row = new Sprite();
         var i:int = 0;
         while(i < 2)
         {
            var b:Shape = new Shape();
            metalBox(b.graphics,rowPts(i),ROW_Y,ROW_H,C_BOX1,C_BOX2,C_RIM,0.85);
            _row.addChild(b);
            _rowBox[i] = b;
            var lb:TextField = text(FONT,S_ROW,C_TEXT);
            spacing(lb,1);
            var v:TextField = text(FONT,S_ROW,C_CLOCK);
            _row.addChild(lb);
            _row.addChild(v);
            _rowLabel[i] = lb;
            _rowVal[i] = v;
            i++;
         }
         _midBox = new Shape();
         _row.addChild(_midBox);
         _midText = text(FONT,S_ROW,C_CLOCK);
         spacing(_midText,1);
         _midTime = text(FONT,S_ROW,C_TEXT);
         _row.addChild(_midText);
         _row.addChild(_midTime);
         _dots = new Shape();
         _row.addChild(_dots);
         _row.visible = SHOTS_ROW;
         _main.addChild(_row);
         paintMid(false);
      }

      private function buildGoal() : void
      {
         _goal = new Sprite();
         _goal.visible = false;
         _goalArt = new Sprite();
         _goalBg = new Shape();
         _goalArt.addChild(_goalBg);
         var clip:Shape = new Shape();
         clip.graphics.beginFill(0);
         poly(clip.graphics,goalPts());
         clip.graphics.endFill();
         _goalArt.addChild(clip);
         _goalBg.mask = clip;
         _goalStrobe = new Shape();
         _goalStrobe.alpha = 0;
         _goalArt.addChild(_goalStrobe);
         _goalSweep = new Shape();
         var sm:Matrix = new Matrix();
         sm.createGradientBox(120,GOAL_H,0,0,0);
         _goalSweep.graphics.beginGradientFill(GradientType.LINEAR,[16777215,16777215,16777215],[0,0.55,0],[0,128,255],sm);
         poly(_goalSweep.graphics,[40,0,120,0,80,GOAL_H,0,GOAL_H]);
         _goalSweep.graphics.endFill();
         _goalSweep.x = -140;
         _goalArt.addChild(_goalSweep);
         _goalRing = new Shape();
         _goalRing.alpha = 0;
         _goalArt.addChild(_goalRing);
         _goalLogoBox = new Sprite();
         _goalLogoBox.y = Math.round(GOAL_H / 2);
         var i:int = 0;
         while(i < 2)
         {
            var gh:Sprite = logoHolder(GOAL_LOGO);
            gh.x = -GOAL_LOGO / 2;
            gh.y = -GOAL_LOGO / 2;
            gh.visible = false;
            _goalLogoBox.addChild(gh);
            _goalLogoHolder[i] = gh;
            i++;
         }
         _goalWord = new Sprite();
         _goalLine = new Shape();
         _goalWord.addChild(_goalLine);
         var word:String = L_GOAL;
         var widths:Array = [];
         var total:Number = 0;
         var gap:Number = S_GOAL * 0.12;
         i = 0;
         while(i < word.length)
         {
            var mt:TextField = text(FONT_HEAVY,S_GOAL,C_TEXT);
            setFieldText(mt,word.charAt(i),C_TEXT);
            widths[i] = word.charAt(i) == " " ? S_GOAL * 0.3 : mt.width - 4;
            total += widths[i] + (i > 0 ? gap : 0);
            i++;
         }
         var cur:Number = BW / 2 - total / 2;
         _wordLeft = cur;
         _wordW = total;
         i = 0;
         while(i < word.length)
         {
            var t:TextField = text(FONT_HEAVY,S_GOAL,C_TEXT);
            setFieldText(t,word.charAt(i),C_TEXT);
            var inner:Sprite = new Sprite();
            inner.addChild(t);
            t.x = -t.width / 2;
            t.y = -t.height / 2;
            inner.transform.matrix = new Matrix(1,0,-0.2,1,0,0);
            var ls:Sprite = new Sprite();
            ls.addChild(inner);
            _letterX[i] = Math.round(cur + widths[i] / 2);
            ls.x = _letterX[i];
            cur += widths[i] + gap;
            ls.y = LETTER_Y;
            ls.visible = false;
            _goalWord.addChild(ls);
            _goalLetters[i] = ls;
            i++;
         }
         _goalArt.addChild(_goalWord);
         _goalArt.addChild(_goalLogoBox);
         _goalPP = text(FONT,S_NAME,C_CLOCK);
         spacing(_goalPP,4);
         _goalPP.visible = false;
         _goalArt.addChild(_goalPP);
         _goalName = text(FONT,S_NAME,C_CLOCK);
         spacing(_goalName,5);
         _goalArt.addChild(_goalName);
         _goal.addChild(_goalArt);
         _goalMask = new Shape();
         _goalMask.graphics.beginFill(0);
         _goalMask.graphics.drawRect(-BW / 2,-40,BW,GOAL_H + 80);
         _goalMask.graphics.endFill();
         _goalMask.x = BW / 2;
         _goalMask.scaleX = 0;
         _goal.addChild(_goalMask);
         _goalArt.mask = _goalMask;
         _main.addChild(_goal);
      }
      // ---------------------------------------------------------------- painting

      private function goalPts() : Array
      {
         return [SLANT,0,BW - SLANT,0,BW,TOP,BW,GOAL_H,0,GOAL_H,0,TOP];
      }

      // the gunmetal base with the team colour shining through (radial glow + diagonal light streaks)
      private function paintGlowPanel(g:Graphics, pts:Array, h:Number, glowX:Number, glowW:Number, c:uint, c2:uint, dir:Number, strength:Number, tint:Number = 0) : void
      {
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(BW,h,Math.PI / 2,0,0);
         g.beginGradientFill(GradientType.LINEAR,[mix(C_FRAME1,c,tint),mix(C_FRAME2,c,tint * 0.6)],[1,1],[0,255],m);
         poly(g,pts);
         g.endFill();
         var rm:Matrix = new Matrix();
         rm.createGradientBox(glowW,h * 1.7,0,glowX - glowW / 2,-h * 0.25);
         g.beginGradientFill(GradientType.RADIAL,[c2,c,c],[0.85 * strength,0.45 * strength,0],[0,110,255],rm);
         g.drawRect(glowX - glowW / 2,-h * 0.25,glowW,h * 1.7);
         g.endFill();
         if(STREAKS)
         {
            var x:Number = -h;
            var n:int = 0;
            while(x < BW + h)
            {
               g.lineStyle(n % 3 == 0 ? 2.5 : 1.5,n % 3 == 0 ? c2 : c,n % 3 == 0 ? 0.75 * strength : 0.45 * strength);
               g.moveTo(x,h);
               g.lineTo(x + dir * h * 0.55,0);
               x += n % 2 == 0 ? 19 : 27;
               n++;
            }
            g.lineStyle();
         }
         var bm:Matrix = new Matrix();
         bm.createGradientBox(BW,3,0,0,h - 7);
         g.beginGradientFill(GradientType.LINEAR,[c2,16777215,c2],[0,0.9 * strength,0],[20,128,235],bm);
         g.drawRect(0,h - 7,BW,2.5);
         g.endFill();
         g.lineStyle(2,C_RIM,0.95,true);
         poly(g,pts);
         g.lineStyle(1,16777215,0.12,true);
         g.moveTo(pts[0] + 2,pts[1] + 3);
         g.lineTo(pts[2] - 2,pts[3] + 3);
         g.lineStyle();
      }

      private function repaint() : void
      {
         var i:int = 0;
         while(i < 2)
         {
            var c:uint = teamPri(i);
            var c2:uint = teamGlow(i);
            var cx:Number = i == 0 ? 150 : BW - 150;
            paintGlowPanel(Shape(_wingArt[i]).graphics,wingPts(i),TOP,cx,300,c,c2,i == 0 ? 1 : -1,GLOW);
            var ag:Graphics = Shape(_accents[i]).graphics;
            ag.clear();
            var k:int = 0;
            while(k < 2)
            {
               var o:Number = 4 + k * 10;
               ag.beginFill(c2,k == 0 ? 1 : 0.6);
               if(i == 0)
               {
                  poly(ag,[o + 18,10,o + 22,10,o + 4,TOP - 10,o,TOP - 10]);
               }
               else
               {
                  poly(ag,[BW - o - 22,10,BW - o - 18,10,BW - o,TOP - 10,BW - o - 4,TOP - 10]);
               }
               ag.endFill();
               k++;
            }
            Shape(_accents[i]).filters = [new GlowFilter(c2,0.9,10,10,2,1)];
            Sprite(_logo[i]).filters = [new GlowFilter(c2,0.7,12,12,1.5,1),new DropShadowFilter(1,90,0,0.8,2,2,1)];
            i++;
         }
         layoutWings();
         if(_tabOpen)
         {
            paintTab();
         }
      }

      private function layoutWings() : void
      {
         var i:int = 0;
         while(i < 2)
         {
            var ab:TextField = _abbr[i];
            setFieldText(ab,String(_names[i]).toUpperCase(),C_TEXT);
            fit(ab,96);
            centerY(ab,0,TOP);
            ab.x = i == 0 ? 110 : Math.round(BW - 110 - ab.width);
            var sc:TextField = _score[i];
            var win:Boolean = _final && Number(_scores[i]) > Number(_scores[1 - i]);
            setFieldText(sc,_scores[i],win ? C_CLOCK : C_TEXT);
            fit(sc,46);
            centerY(sc,0,TOP);
            sc.x = i == 0 ? Math.round(WING - 26 - sc.width) : BW - WING + 26;
            i++;
         }
      }

      private function paintMid(alert:Boolean) : void
      {
         var g:Graphics = _midBox.graphics;
         g.clear();
         metalBox(g,rowPts(2),ROW_Y,ROW_H,C_BOX1,C_BOX2,alert ? C_PENALTY : C_RIM,alert ? 1 : 0.85);
         _midBox.filters = alert ? [new GlowFilter(C_PENALTY,0.55,10,10,1.5,1)] : [];
      }

      private function layoutRow(midLabel:String, midTime:String, alert:Boolean) : void
      {
         _midShown = midLabel != null && midLabel != "" && midLabel != L_MID;
         _midBox.visible = _midText.visible = _midShown;
         var label:String = _rowName == null || _rowName == "" || _rowName.indexOf("SHOT") >= 0 ? L_SHOTS : _rowName;
         var i:int = 0;
         while(i < 2)
         {
            var lb:TextField = _rowLabel[i];
            var v:TextField = _rowVal[i];
            setFieldText(lb,label,C_TEXT);
            spacing(lb,1);
            setFieldText(v,_shots[i],C_CLOCK);
            lb.visible = v.visible = false;
            Shape(_rowBox[i]).visible = _isShootout;
            var pts:Array = rowPts(i);
            var x0:Number = i == 0 ? 52 : pts[0];
            var w:Number = i == 0 ? CX - 2 - 52 : BW - 52 - pts[0];
            var tw:Number = lb.width + 8 + v.width;
            lb.x = Math.round(x0 + (w - tw) / 2);
            v.x = Math.round(lb.x + lb.width + 8);
            centerY(lb,ROW_Y,ROW_H);
            centerY(v,ROW_Y,ROW_H);
            i++;
         }
         paintMid(alert);
         setFieldText(_midText,midLabel,C_CLOCK);
         spacing(_midText,1);
         var hasTime:Boolean = midTime != null && midTime != "";
         setFieldText(_midTime,hasTime ? midTime : "",C_TEXT);
         _midTime.visible = hasTime && _midShown;
         var room:Number = CW - 30;
         _midText.scaleX = _midText.scaleY = _midTime.scaleX = _midTime.scaleY = 1;
         var total:Number = _midText.width + (hasTime ? _midTime.width + 8 : 0);
         if(total > room)
         {
            var s:Number = room / total;
            _midText.scaleX = _midText.scaleY = _midTime.scaleX = _midTime.scaleY = s;
            total = room;
         }
         _midText.x = Math.round(CX + (CW - total) / 2);
         _midTime.x = Math.round(_midText.x + _midText.width + 8);
         centerY(_midText,ROW_Y,ROW_H);
         centerY(_midTime,ROW_Y,ROW_H);
         _dots.visible = _isShootout;
      }

      private function paintTab() : void
      {
         var i:int = _tabTeam;
         if(i < 0)
         {
            return;
         }
         var c:uint = teamPri(i);
         var c2:uint = teamGlow(i);
         var pts:Array = rowPts(i);
         var x0:Number = i == 0 ? 52 : pts[0];
         var w:Number = i == 0 ? CX - 2 - 52 : BW - 52 - pts[0];
         var g:Graphics = _tabBg.graphics;
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(w,ROW_H,Math.PI / 2,x0,ROW_Y);
         g.lineStyle(1.5,c2,1,true);
         g.beginGradientFill(GradientType.LINEAR,[C_BOX1,C_BOX2],[1,1],[0,255],m);
         poly(g,pts);
         g.endFill();
         g.lineStyle();
         var rm:Matrix = new Matrix();
         rm.createGradientBox(w,ROW_H * 2,0,x0,ROW_Y - ROW_H * 0.5);
         g.beginGradientFill(GradientType.RADIAL,[c2,c],[0.45 * GLOW,0],[0,255],rm);
         poly(g,pts);
         g.endFill();
         _tabBg.filters = [new GlowFilter(c2,0.5,8,8,1,1)];
         var hasTime:Boolean = _tabTime.visible;
         var room:Number = w - 24;
         _tabLabel.scaleX = _tabLabel.scaleY = _tabTime.scaleX = _tabTime.scaleY = 1;
         var total:Number = _tabLabel.width + (hasTime ? _tabTime.width + 10 : 0);
         if(total > room)
         {
            var s:Number = room / total;
            _tabLabel.scaleX = _tabLabel.scaleY = _tabTime.scaleX = _tabTime.scaleY = s;
            total = room;
         }
         _tabLabel.x = Math.round(x0 + (w - total) / 2);
         _tabTime.x = Math.round(_tabLabel.x + _tabLabel.width + 10);
         centerY(_tabLabel,ROW_Y,ROW_H);
         centerY(_tabTime,ROW_Y,ROW_H);
      }

      // the logo sits on the scoring team's side, the team name on the other
      private function paintGoal(i:int) : void
      {
         var c:uint = teamPri(i);
         var c2:uint = teamGlow(i);
         var ink:uint = lum(c) > 0.4 ? C_TEXT : C_CLOCK;
         paintGlowPanel(_goalBg.graphics,goalPts(),GOAL_H,BW / 2,BW * 0.9,c,c2,i == 0 ? 1 : -1,Math.min(1,GLOW + 0.15),0.5);
         _goalBg.filters = [new GlowFilter(c2,0.5,14,14,1,1)];
         var sg:Graphics = _goalStrobe.graphics;
         sg.clear();
         sg.beginFill(mix(c2,16777215,0.35));
         poly(sg,goalPts());
         sg.endFill();
         var rg:Graphics = _goalRing.graphics;
         rg.clear();
         rg.lineStyle(5,c2,1);
         rg.drawCircle(0,0,GOAL_LOGO * 0.5);
         rg.lineStyle(2,16777215,0.8);
         rg.drawCircle(0,0,GOAL_LOGO * 0.5 - 6);
         _goalRing.filters = [new GlowFilter(c2,0.9,16,16,2,1)];
         var lg:Graphics = _goalLine.graphics;
         lg.clear();
         lg.beginFill(ink);
         lg.drawRect(0,0,_wordW,4);
         lg.endFill();
         _goalLine.x = _wordLeft;
         _goalLine.y = LETTER_Y + S_GOAL * 0.42;
         _goalLine.filters = [new GlowFilter(ink,0.8,8,8,2,1)];
         var k:int = 0;
         while(k < 2)
         {
            Sprite(_goalLogoHolder[k]).visible = k == i;
            k++;
         }
         _goalLogoBox.filters = [new GlowFilter(c2,0.8,18,18,2,1),new DropShadowFilter(3,90,0,0.6,6,6,1)];
         k = 0;
         while(k < _goalLetters.length)
         {
            Sprite(_goalLetters[k]).filters = [new GlowFilter(c2,0.8,14,14,2,1),new DropShadowFilter(3,90,0,0.7,4,4,1)];
            k++;
         }
         _logoRestX = i == 0 ? 44 + GOAL_LOGO / 2 : BW - 44 - GOAL_LOGO / 2;
         _goalWord.x = i == 0 ? 46 : -46;
         setFieldText(_goalName,nickname(i),ink);
         spacing(_goalName,5);
         fit(_goalName,200);
         _goalName.x = i == 0 ? Math.round(BW - 64 - _goalName.width) : 64;
         _goalName.y = GOAL_H - _goalName.height - 8;
         setFieldText(_goalPP,L_PP,ink);
         spacing(_goalPP,4);
         _goalPP.x = Math.round(BW / 2 + _goalWord.x - _goalPP.width / 2);
         _goalPP.y = 6;
      }
      // ---------------------------------------------------------------- public API

      public function init(param1:Object, param2:Function = null, param3:Function = null) : void
      {
         _audioCallBack = param2;
         _signalCallBack = param3;
         if(param1 == null)
         {
            return;
         }
         if(param1.teamColorAway != null)
         {
            _primary[0] = uint(param1.teamColorAway);
         }
         if(param1.teamColorHome != null)
         {
            _primary[1] = uint(param1.teamColorHome);
         }
         if(param1.teamSecondaryColorAway != null)
         {
            _secondary[0] = uint(param1.teamSecondaryColorAway);
         }
         if(param1.teamSecondaryColorHome != null)
         {
            _secondary[1] = uint(param1.teamSecondaryColorHome);
         }
         repaint();
         loadLogo(_logo[0],param1.logoPathAway);
         loadLogo(_logo[1],param1.logoPathHome);
         loadLogo(_goalLogoHolder[0],param1.logoPathAway);
         loadLogo(_goalLogoHolder[1],param1.logoPathHome);
      }

      private function loadLogo(holder:Sprite, path:String) : void
      {
         var clip:MovieClip;
         var loaderClass:Class;
         var l:Loader;
         while(holder.numChildren > 0)
         {
            holder.removeChildAt(0);
         }
         if(path == null || path == "")
         {
            return;
         }
         // the game's loader resets its clip's scale whenever it redraws, so the sizing goes on a wrapper it never touches
         var wrap:Sprite = new Sprite();
         // start at the size a 256 px game logo needs, so a reload never shows a frame at full size
         for each(var fo:Object in _fits)
         {
            if(fo.h == holder)
            {
               wrap.scaleX = wrap.scaleY = Number(fo.s) / 256;
               fo.k = "";
            }
         }
         holder.addChild(wrap);
         clip = new MovieClip();
         wrap.addChild(clip);
         loaderClass = null;
         try
         {
            loaderClass = getDefinitionByName("loader.ImageLoader") as Class;
         }
         catch(e:Error)
         {
            loaderClass = null;
         }
         if(loaderClass != null)
         {
            try
            {
               new loaderClass(clip,path,{
                  "x":0,
                  "y":0
               });
               return;
            }
            catch(e2:Error)
            {
            }
         }
         l = new Loader();
         clip.addChild(l);
         try
         {
            l.load(new URLRequest(path));
         }
         catch(e3:Error)
         {
         }
      }

      public function setIsDebug(param1:Boolean) : void
      {
         _isDebug = param1;
      }

      public function get isDebug() : Boolean
      {
         return _isDebug;
      }

      public function set isDebug(param1:Boolean) : void
      {
         _isDebug = param1;
      }

      public function get state() : Number
      {
         return _state;
      }

      public function set state(param1:Number) : void
      {
         _state = param1;
      }

      public function setAwayTeamName(param1:String) : void
      {
         _names[0] = param1;
         repaint();
      }

      public function setHomeTeamName(param1:String) : void
      {
         _names[1] = param1;
         repaint();
      }

      public function setScores(param1:Array, param2:Boolean = false) : void
      {
         if(param1 == null || param1.length < 2)
         {
            return;
         }
         var changed:Array = [String(param1[0]) != _scores[0],String(param1[1]) != _scores[1]];
         _scores[0] = String(param1[0]);
         _scores[1] = String(param1[1]);
         layoutWings();
         var i:int = 0;
         while(i < 2)
         {
            if(changed[i] && !_instant && (_goalUp || _goalPending))
            {
               _pendingPulse[i] = true;
            }
            else if(changed[i] && param2)
            {
               pulseScore(i);
            }
            i++;
         }
      }

      public function setScoresWithDelay(param1:Array, param2:Boolean = false, param3:Number = 0) : void
      {
         if(param3 > 0)
         {
            later(param3,"scores",[param1,param2]);
         }
         else
         {
            setScores(param1,param2);
         }
      }

      // "1ST" -> "1st PERIOD", "OT" -> "OVERTIME"; anything else as the game sends it
      private static function periodLabel(s:String) : String
      {
         if(s.length == 3 && allDigits(s.charAt(0)))
         {
            var suf:String = s.substr(1);
            if(suf == "ST" || suf == "ND" || suf == "RD" || suf == "TH")
            {
               return s.charAt(0) + suf.toLowerCase() + " " + L_PERIOD;
            }
         }
         if(s == "OT")
         {
            return L_OT;
         }
         if(s == "SO")
         {
            return L_SHOOTOUT;
         }
         return s;
      }

      public function setPeriod(param1:String) : void
      {
         var s:String = param1 == null ? "" : trim(param1).toUpperCase();
         _final = s.indexOf("FINAL") >= 0;
         _period = s;
         setFieldText(_periodText,_final ? (s == "FINAL" ? L_FINAL : s) : periodLabel(s),_final ? C_CLOCK : C_TEXT);
         layoutClock();
         layoutWings();
      }

      public function setTime(param1:String) : void
      {
         setFieldText(_clockText,param1,C_CLOCK);
         layoutClock();
      }

      private function layoutClock() : void
      {
         if(_periodText == null || _clockText == null)
         {
            return;
         }
         var practice:Boolean = _practiceText.visible;
         _clockText.visible = !_final && !practice;
         _periodText.visible = !practice;
         fit(_clockText,CW - 34);
         _clockText.x = Math.round(CX + (CW - _clockText.width) / 2);
         _clockText.y = Math.round(18 - RAISE + 8);
         if(_final)
         {
            _periodText.scaleX = _periodText.scaleY = 1;
            var fs:Number = Math.min(S_CLOCK * 0.9 / S_PERIOD,(CW - 36) / Math.max(1,_periodText.width));
            _periodText.scaleX = _periodText.scaleY = fs;
            _periodText.x = Math.round(CX + (CW - _periodText.width) / 2);
            centerY(_periodText,-RAISE,TOP + RAISE - 8);
            _periodText.filters = [new GlowFilter(C_CLOCK,0.35,8,8,1,1)];
         }
         else
         {
            fit(_periodText,CW - 30);
            _periodText.x = Math.round(CX + (CW - _periodText.width) / 2);
            _periodText.y = Math.round(-RAISE + 6);
            _periodText.filters = [];
         }
      }

      public function setPracticeText(param1:String) : void
      {
         var on:Boolean = param1 != null && param1 != "";
         _practiceText.visible = on;
         if(on)
         {
            setFieldText(_practiceText,param1,C_TEXT);
            fit(_practiceText,CW - 30);
            _practiceText.x = Math.round(CX + (CW - _practiceText.width) / 2);
            centerY(_practiceText,-RAISE,TOP + RAISE - 8);
         }
         layoutClock();
      }

      public function setRivalsMedallion(param1:Boolean) : void
      {
      }

      public function showClock(param1:Boolean = false) : void
      {
         _visible = true;
         _bug.visible = true;
         stopTweens(_bug);
         if(param1)
         {
            _bug.alpha = 1;
            _bug.y = 0;
         }
         else
         {
            if(_bug.alpha < 0.01)
            {
               _bug.y = -24;
            }
            tween(_bug,"alpha",1,0.3);
            tween(_bug,"y",0,0.35);
         }
         _state = 1;
      }

      public function hideClock(param1:Boolean = false) : void
      {
         _visible = false;
         stopTweens(_bug);
         if(param1)
         {
            _bug.alpha = 0;
            _bug.visible = false;
         }
         else
         {
            tween(_bug,"alpha",0,0.25,0,"hideBug");
            tween(_bug,"y",-24,0.25);
         }
         _state = 0;
      }

      public function showGoal(param1:Number) : void
      {
         showGoalWithDelay(param1,0);
      }

      public function showGoalWithDelay(param1:Number, param2:Number = 0) : void
      {
         if(param2 > 0)
         {
            later(param2,"goal",[param1]);
         }
         else
         {
            startGoal(param1);
         }
      }

      public function showTimedGoal(param1:Number) : void
      {
         startGoal(param1);
         autoLater(TIMED_GOAL_HOLD + 1.5,"autoGoal",[_goalGen]);
      }

      // ---------------------------------------------------------------- goal

      private function startGoal(side:Number) : void
      {
         var i:int = sideIndex(side);
         if(i < 0)
         {
            return;
         }
         if(!_visible)
         {
            showClock();
         }
         _goalIsPP = isPowerPlay() && _warnSide == side;
         _goalSide = side;
         ++_goalGen;
         playSfx(side == AWAY ? AUDIO_GOAL_AWAY : AUDIO_GOAL_HOME);
         refreshBar();
         closeSide(true);
         _goalPending = true;
         _goalStart = getTimer();
         if(_instant)
         {
            goalIn();
            goalSnapHeld();
            return;
         }
         later(0.05,"goalIn",[_goalGen]);
         autoLater(GOAL_MAX_HOLD,"autoGoal",[_goalGen]);
      }

      private function goalReset() : void
      {
         stopTweens(_goalMask);
         stopTweens(_goalSweep);
         stopTweens(_goalLogoBox);
         stopTweens(_goalPP);
         stopTweens(_goalName);
         stopTweens(_goalRing);
         stopTweens(_goalStrobe);
         stopTweens(_goalLine);
         _goalMask.scaleX = 0;
         _goalMask.x = BW / 2;
         _goalSweep.x = -140;
         _goalLogoBox.x = _logoRestX;
         _goalLogoBox.y = Math.round(GOAL_H / 2);
         _goalLogoBox.scaleX = _goalLogoBox.scaleY = 0.4;
         _goalLogoBox.alpha = 0;
         _goalRing.alpha = 0;
         _goalStrobe.alpha = 0;
         _goalLine.scaleX = 0;
         _goalLine.alpha = 1;
         _goalName.alpha = 0;
         var k:int = 0;
         while(k < _goalLetters.length)
         {
            var ls:Sprite = _goalLetters[k];
            stopTweens(ls);
            ls.visible = false;
            ls.alpha = 0;
            ls.scaleX = ls.scaleY = 1;
            ls.x = _letterX[k];
            ls.y = LETTER_Y;
            k++;
         }
         _goalPP.visible = false;
      }

      // 1 burst, 2 slash, 3 spotlight, 4 strobe; goalStyle 0 takes turns goal by goal
      private function goalIn() : void
      {
         var i:int = sideIndex(_goalSide);
         if(i < 0)
         {
            return;
         }
         _goalIdx = i;
         ++_goalCount;
         _style = GOAL_STYLE >= 1 && GOAL_STYLE <= 4 ? GOAL_STYLE : (_goalCount - 1) % 4 + 1;
         paintGoal(i);
         goalReset();
         _goal.visible = true;
         _goalUp = true;
         _goalPending = false;
         var g:int = _goalGen;
         if(_instant)
         {
            return;
         }
         var rx:Number = _logoRestX;
         var dir:Number = i == 0 ? -1 : 1;
         var lettersAt:Number = 0.55;
         var nameAt:Number = 0.6;
         if(_style == 2)
         {
            // SLASH: the panel slices in from the scorer's side, the logo rides in with it, GOAL slides in against it
            _goalMask.scaleX = 1;
            _goalMask.x = i == 0 ? -BW / 2 : BW * 1.5;
            tween(_goalMask,"x",BW / 2,0.3);
            later(0.3,"gCover",[g]);
            _goalLogoBox.x = rx + dir * 300;
            _goalLogoBox.scaleX = _goalLogoBox.scaleY = 1;
            tween(_goalLogoBox,"alpha",1,0.12,0.05);
            tween(_goalLogoBox,"x",rx,0.42,0.05);
            tween(_goalSweep,"x",BW + 20,0.45,0.25);
            lettersAt = 0.32;
            nameAt = 0.75;
         }
         else if(_style == 3)
         {
            // SPOTLIGHT: the logo lands huge in the middle with a shockwave ring, then steps aside for GOAL
            tween(_goalMask,"scaleX",1,0.18);
            later(0.18,"gCover",[g]);
            _goalLogoBox.x = BW / 2;
            _goalLogoBox.scaleX = _goalLogoBox.scaleY = 2.6;
            tween(_goalLogoBox,"alpha",1,0.14,0.1);
            tween(_goalLogoBox,"scaleX",1.3,0.32,0.1);
            tween(_goalLogoBox,"scaleY",1.3,0.32,0.1);
            _goalRing.x = BW / 2;
            _goalRing.y = GOAL_H / 2;
            _goalRing.scaleX = _goalRing.scaleY = 0.6;
            later(0.38,"gRing",[g]);
            later(1.05,"gSpot",[g]);
            lettersAt = 1.15;
            nameAt = 1.4;
         }
         else if(_style == 4)
         {
            // STROBE: the panel snaps on and flashes like arena lights, GOAL types out over a drawn underline
            tween(_goalMask,"scaleX",1,0.1);
            later(0.1,"gCover",[g]);
            var k:int = 0;
            while(k < 3)
            {
               tween(_goalStrobe,"alpha",0.8,0.04,0.1 + k * 0.2);
               tween(_goalStrobe,"alpha",0,0.14,0.15 + k * 0.2);
               k++;
            }
            _goalLogoBox.scaleX = _goalLogoBox.scaleY = 1;
            _goalLogoBox.y = GOAL_H / 2 + 40;
            tween(_goalLogoBox,"alpha",1,0.2,0.7);
            tween(_goalLogoBox,"y",Math.round(GOAL_H / 2),0.35,0.7);
            lettersAt = 0.75;
            nameAt = 1.2;
         }
         else
         {
            // BURST: the panel bursts open from the clock, the logo punches in, GOAL slams down letter by letter
            tween(_goalMask,"scaleX",1,0.26);
            later(0.26,"gCover",[g]);
            tween(_goalSweep,"x",BW + 20,0.55,0.18);
            tween(_goalLogoBox,"alpha",1,0.2,0.3);
            tween(_goalLogoBox,"scaleX",1,0.35,0.3);
            tween(_goalLogoBox,"scaleY",1,0.35,0.3);
         }
         tween(_goalName,"alpha",1,0.3,nameAt);
         later(lettersAt,"gLetters",[g]);
         later(lettersAt + 1.2,"gSweep",[g]);
      }

      private function goalCover() : void
      {
         _covered = true;
         var i:int = 0;
         while(i < 2)
         {
            stopTweens(_wings[i]);
            Sprite(_wings[i]).alpha = 0;
            stopTweens(_accents[i]);
            Shape(_accents[i]).alpha = 0;
            i++;
         }
         stopTweens(_center);
         _center.alpha = 0;
         stopTweens(_row);
         _row.alpha = 0;
         stopTweens(_frame);
         _frame.alpha = 0;
      }

      private function goalSnapHeld() : void
      {
         goalCover();
         _goalMask.scaleX = 1;
         _goalMask.x = BW / 2;
         _goalLogoBox.x = _logoRestX;
         _goalLogoBox.alpha = 1;
         _goalLogoBox.scaleX = _goalLogoBox.scaleY = 1;
         _goalName.alpha = 1;
         _goalLine.scaleX = _style == 4 ? 1 : 0;
         var k:int = 0;
         while(k < _goalLetters.length)
         {
            var ls:Sprite = _goalLetters[k];
            ls.visible = true;
            ls.alpha = 1;
            k++;
         }
         _goalPP.visible = _goalIsPP;
         _goalPP.alpha = 1;
      }

      private function goalLetters() : void
      {
         var n:int = _goalLetters.length;
         var k:int = 0;
         while(k < n)
         {
            var ls:Sprite = _goalLetters[k];
            ls.visible = true;
            ls.alpha = 0;
            if(_style == 2)
            {
               var from:Number = _goalIdx == 0 ? 140 : -140;
               var kk:int = _goalIdx == 0 ? k : n - 1 - k;
               ls.x = _letterX[k] + from;
               tween(ls,"alpha",1,0.12,kk * 0.06);
               tween(ls,"x",_letterX[k],0.3,kk * 0.06);
            }
            else if(_style == 3)
            {
               ls.y = LETTER_Y - 90;
               tween(ls,"alpha",1,0.08,k * 0.08);
               tween(ls,"y",LETTER_Y + 7,0.16,k * 0.08);
               tween(ls,"y",LETTER_Y,0.14,k * 0.08 + 0.16);
            }
            else if(_style == 4)
            {
               ls.scaleX = ls.scaleY = 1.25;
               tween(ls,"alpha",1,0.02,k * 0.1);
               tween(ls,"scaleX",1,0.12,k * 0.1);
               tween(ls,"scaleY",1,0.12,k * 0.1);
            }
            else
            {
               ls.scaleX = ls.scaleY = 1.9;
               tween(ls,"alpha",1,0.1,k * 0.07);
               tween(ls,"scaleX",1,0.18,k * 0.07);
               tween(ls,"scaleY",1,0.18,k * 0.07);
            }
            k++;
         }
         if(_style == 4)
         {
            tween(_goalLine,"scaleX",1,n * 0.1 + 0.1);
         }
         if(_goalIsPP)
         {
            _goalPP.visible = true;
            _goalPP.alpha = 0;
            tween(_goalPP,"alpha",1,0.2,0.35);
         }
      }

      private function goalOut() : void
      {
         _goalUp = false;
         if(_instant)
         {
            goalOff();
            afterGoal();
            return;
         }
         var k:int = 0;
         while(k < _goalLetters.length)
         {
            var ls:Sprite = _goalLetters[k];
            stopTweens(ls);
            tween(ls,"alpha",0,0.12,k * 0.04);
            k++;
         }
         stopTweens(_goalPP);
         tween(_goalPP,"alpha",0,0.1);
         stopTweens(_goalLogoBox);
         tween(_goalLogoBox,"alpha",0,0.15);
         tween(_goalName,"alpha",0,0.12);
         stopTweens(_goalLine);
         tween(_goalLine,"alpha",0,0.12);
         stopTweens(_goalMask);
         if(_style == 2)
         {
            tween(_goalMask,"x",_goalIdx == 0 ? BW * 1.5 : -BW / 2,0.3,0.1);
         }
         else
         {
            tween(_goalMask,"scaleX",0.01,0.28,0.12);
         }
         later(0.4,"gRebuild",[_goalGen]);
      }
      private function goalOff() : void
      {
         _goal.visible = false;
         _goalUp = false;
         goalReset();
         _covered = false;
         var i:int = 0;
         while(i < 2)
         {
            stopTweens(_wings[i]);
            Sprite(_wings[i]).alpha = 1;
            stopTweens(_accents[i]);
            Shape(_accents[i]).alpha = 1;
            stopTweens(_score[i]);
            TextField(_score[i]).alpha = 1;
            i++;
         }
         stopTweens(_center);
         _center.alpha = 1;
         stopTweens(_row);
         _row.alpha = 1;
         stopTweens(_frame);
         _frame.alpha = 1;
         layoutWings();
      }

      private function goalRebuild() : void
      {
         _goal.visible = false;
         goalReset();
         _covered = false;
         tween(_frame,"alpha",1,0.14);
         tween(_center,"alpha",1,0.16);
         var i:int = 0;
         while(i < 2)
         {
            tween(_wings[i],"alpha",1,0.16,0.06 + i * 0.06);
            tween(_accents[i],"alpha",1,0.2,0.18);
            i++;
         }
         tween(_row,"alpha",1,0.16,0.2);
         later(0.4,"afterGoal",null);
      }

      private function afterGoal() : void
      {
         if(_goalUp || _goalPending)
         {
            return;
         }
         var i:int = 0;
         while(i < 2)
         {
            if(_pendingPulse[i])
            {
               _pendingPulse[i] = false;
               pulseScore(i);
            }
            i++;
         }
         refreshBar();
         if(_pendingSide != null)
         {
            var ps:Array = _pendingSide;
            _pendingSide = null;
            openSide(Number(ps[0]),String(ps[1]),ps[2] == null ? null : String(ps[2]));
         }
      }

      public function hideGoal(param1:Boolean = false) : void
      {
         if(!param1 && !_instant && (_goalUp || _goalPending))
         {
            var left:Number = GOAL_MIN_HOLD - (getTimer() - _goalStart) / 1000;
            if(left > 0.05)
            {
               later(left,"hideGoalNow",[_goalGen]);
               return;
            }
         }
         _goalSide = -1;
         _goalPending = false;
         ++_goalGen;
         if(_goalUp || _covered)
         {
            if(param1 || _instant)
            {
               goalOff();
            }
            else
            {
               goalOut();
            }
         }
         else
         {
            goalOff();
            refreshBar(param1);
            return;
         }
         if(param1 || _instant)
         {
            refreshBar(true);
         }
      }

      // ---------------------------------------------------------------- warnings, popups, dropdowns, comparisons

      public function stopPowerplayLines(param1:Number) : void
      {
      }

      public function showWarning(param1:Number, param2:String, param3:Boolean = false, param4:Boolean = false) : void
      {
         var fresh:Boolean = _warnText == null || _warnSide != param1;
         _warnSide = param1;
         var parts:Array = splitClock(param2 == null ? "" : param2);
         _warnText = parts[0];
         _warnTime = parts[1];
         if(fresh && param1 != NEUTRAL)
         {
            playSfx(param1 == AWAY ? AUDIO_PENALTY_AWAY : AUDIO_PENALTY_HOME);
         }
         refreshBar();
      }

      public function hideWarning(param1:Number, param2:Boolean = false) : void
      {
         if(param1 != _warnSide && param1 != NEUTRAL && _warnSide != NEUTRAL)
         {
            return;
         }
         _warnSide = -1;
         _warnText = null;
         _warnTime = null;
         refreshBar(param2);
      }

      public function updatePowerplayTime(param1:Number, param2:String) : void
      {
         var parts:Array = splitClock(param2 == null ? "" : param2);
         if(_warnText == null || _warnSide != param1)
         {
            // the game only sends showWarning for a power play if its clock already ran when the penalty was called
            // (otherwise not until the next faceoff / pause); open the box on the countdown itself
            if(!(param1 == AWAY || param1 == HOME) || parts[1] == null || String(parts[1]) == "" || /^0?0:00/.test(String(parts[1])))
            {
               return;
            }
            _warnSide = param1;
            _warnText = parts[0];
            _warnTime = String(parts[1]);
            refreshBar();
            return;
         }
         _warnTime = parts[1] != null ? String(parts[1]) : String(parts[0]);
         refreshBar();
      }

      public function showPopup(param1:String) : void
      {
         _popupText = param1;
         refreshBar();
      }

      public function updatePopup(param1:String) : void
      {
         _popupText = param1;
         refreshBar();
      }

      public function hidePopup() : void
      {
         _popupText = null;
         refreshBar();
      }

      public function showDropdown(param1:Number = 0, param2:String = "", param3:Object = null) : void
      {
         var s:String = param2 == null ? "" : param2;
         var s2:String = null;
         if(param3 != null && param3.scorer != null)
         {
            s = String(param3.scorer);
            if(param3.value != null && String(param3.value) != "")
            {
               s += " (" + param3.value + ")";
            }
            if(param3.goalLabel != null && String(param3.goalLabel) != "")
            {
               s = String(param3.goalLabel) + "  " + s;
            }
            if(param3.assists != null && String(param3.assists) != "")
            {
               s2 = (param3.assistsLabel != null ? String(param3.assistsLabel) + ": " : "") + param3.assists;
            }
            else if(param3.assistsLabel != null && String(param3.assistsLabel) != "")
            {
               s2 = String(param3.assistsLabel);
            }
         }
         if(s == "")
         {
            return;
         }
         playSfx(param1 == AWAY ? AUDIO_HIGHLIGHT_AWAY : AUDIO_HIGHLIGHT_HOME);
         if(!_instant && (_goalUp || _goalPending || _covered))
         {
            _pendingSide = [param1,s,s2];
            return;
         }
         openSide(param1,s,s2);
      }

      public function hideDropdown(param1:Number = 0, param2:Boolean = false) : void
      {
         _pendingSide = null;
         closeSide(param2);
      }

      public function showComparison(param1:Array) : void
      {
         if(param1 == null || param1.length < 3)
         {
            return;
         }
         var label:String = String(param1[2]).toUpperCase();
         _shots[0] = String(param1[0]);
         _shots[1] = String(param1[1]);
         _rowName = label;
         if(label == "" || label.indexOf("SHOT") >= 0)
         {
            _lastShots[0] = _shots[0];
            _lastShots[1] = _shots[1];
         }
         else
         {
            ++_shotsGen;
            autoLater(SHOTS_HOLD,"autoShots",[_shotsGen]);
         }
         refreshBar();
      }

      private function backToShots() : void
      {
         _shots[0] = _lastShots[0];
         _shots[1] = _lastShots[1];
         _rowName = null;
         refreshBar();
      }

      public function hideComparison() : void
      {
      }

      public function showShootout(param1:Boolean = false) : void
      {
         _isShootout = true;
         setPeriod("SO");
         drawShootout();
         refreshBar();
      }

      public function hideShootout() : void
      {
         _isShootout = false;
         refreshBar();
      }

      public function updateShootoutData(param1:Object) : void
      {
         _shootout = param1;
         if(_isShootout)
         {
            drawShootout();
            refreshBar();
         }
      }

      public function showComparisonOverlay(param1:Array) : void
      {
         showComparison(param1);
      }

      public function dispose() : void
      {
         removeEventListener(Event.ENTER_FRAME,onFrame);
         _tweens = [];
         _audioCallBack = null;
         _signalCallBack = null;
      }

      private function isPowerPlay() : Boolean
      {
         return _warnText != null && _warnTime != null && _warnTime != "" && (_warnSide == AWAY || _warnSide == HOME);
      }

      private function shootoutRound() : int
      {
         if(_shootout == null)
         {
            return 1;
         }
         var n:int = _shootout.numRounds != null ? int(_shootout.numRounds) : 3;
         var r:int = 0;
         var k:int = 1;
         while(k <= Math.max(n,30))
         {
            var a:Number = Number(_shootout["awayRound" + k]);
            var h:Number = Number(_shootout["homeRound" + k]);
            if(a > 0 && h > 0)
            {
               r = k;
            }
            k++;
         }
         return r + 1;
      }

      // what the tab under a team and the middle of the shots row show now
      private function refreshBar(immediate:Boolean = false) : void
      {
         var tabText:String = null;
         var tabTime:String = null;
         var tabTeam:int = -1;
         var midText:String = L_MID;
         var midTime:String = null;
         var alert:Boolean = false;
         if(!(_goalSide == AWAY || _goalSide == HOME || _covered))
         {
            if(_warnText != null)
            {
               var wi:int = sideIndex(_warnSide);
               if(wi >= 0)
               {
                  tabTeam = wi;
                  tabText = _warnText == "" ? L_PP : _warnText.toUpperCase();
                  tabTime = _warnTime;
               }
               else
               {
                  midText = _warnText.toUpperCase();
                  midTime = _warnTime;
                  alert = true;
               }
            }
            if(!alert && _popupText != null && _popupText != "")
            {
               var pp:Array = splitClock(_popupText);
               midText = String(pp[0]).toUpperCase();
               midTime = pp[1];
            }
            if(!alert && _isShootout && (_popupText == null || _popupText == ""))
            {
               midText = L_SHOOTOUT + " " + L_ROUND + " " + shootoutRound();
            }
         }
         layoutRow(midText,midTime,alert);
         if(tabText == null)
         {
            closeTab(immediate);
         }
         else
         {
            openTab(tabTeam,tabText,tabTime,immediate);
         }
         placeSide(immediate);
      }

      private function openTab(team:int, label:String, time:String, immediate:Boolean) : void
      {
         var moved:Boolean = _tabTeam != team;
         _tabTeam = team;
         setFieldText(_tabLabel,label,C_CLOCK);
         spacing(_tabLabel,1);
         var hasTime:Boolean = time != null && time != "";
         setFieldText(_tabTime,hasTime ? time : "",C_TEXT);
         _tabTime.visible = hasTime;
         paintTab();
         _tab.visible = true;
         stopTweens(_tab);
         if(immediate || _instant)
         {
            _tab.y = 0;
            _tab.alpha = 1;
         }
         else if(!_tabOpen || moved)
         {
            _tab.y = -ROW_H;
            _tab.alpha = 0;
            tween(_tab,"y",0,0.25);
            tween(_tab,"alpha",1,0.2);
         }
         _tabOpen = true;
      }

      private function closeTab(immediate:Boolean) : void
      {
         if(!_tabOpen && !immediate)
         {
            return;
         }
         _tabOpen = false;
         stopTweens(_tab);
         if(immediate || _instant)
         {
            _tab.y = -ROW_H;
            _tab.alpha = 0;
            _tab.visible = false;
         }
         else
         {
            tween(_tab,"y",-ROW_H,0.2,0,"hideTab");
            tween(_tab,"alpha",0,0.2);
         }
      }

      private function sideY() : Number
      {
         return (_tabOpen || _midShown || _isShootout ? ROW_Y + ROW_H : TOP) + 4;
      }

      private function placeSide(immediate:Boolean) : void
      {
         if(!_sideOpen)
         {
            return;
         }
         var y:Number = sideY();
         if(immediate || _instant)
         {
            _side.y = y;
         }
         else if(Math.abs(_side.y - y) > 0.5)
         {
            tween(_side,"y",y,0.25);
         }
      }

      // scorer / stat line: a panel that drops below the bar, centred, glowing in the team's colour
      private function openSide(side:Number, s:String, s2:String) : void
      {
         var i:int = sideIndex(side);
         var c:uint = i >= 0 ? teamPri(i) : C_RIM;
         var c2:uint = i >= 0 ? teamGlow(i) : C_CLOCK;
         setFieldText(_sideText,s.toUpperCase(),C_TEXT);
         var two:Boolean = s2 != null && s2 != "";
         setFieldText(_sideText2,two ? s2.toUpperCase() : "",C_CLOCK);
         _sideText2.visible = two;
         fit(_sideText,BW - 80);
         fit(_sideText2,BW - 80);
         var w:Number = Math.round(Math.max(_sideText.width,two ? _sideText2.width : 0) + 60);
         var h:Number = two ? 50 : 32;
         var x0:Number = Math.round((BW - w) / 2);
         var pts:Array = [x0,0,x0 + w,0,x0 + w - 14,h,x0 + 14,h];
         var g:Graphics = _sideBg.graphics;
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(w,h,Math.PI / 2,x0,0);
         g.lineStyle(1.5,c2,1,true);
         g.beginGradientFill(GradientType.LINEAR,[C_BOX1,C_BOX2],[1,1],[0,255],m);
         poly(g,pts);
         g.endFill();
         g.lineStyle();
         var rm:Matrix = new Matrix();
         rm.createGradientBox(w,h * 2,0,x0,-h * 0.5);
         g.beginGradientFill(GradientType.RADIAL,[c2,c],[0.35 * GLOW,0],[0,255],rm);
         poly(g,pts);
         g.endFill();
         _sideBg.filters = [new GlowFilter(c2,0.45,8,8,1,1)];
         _sideText.x = Math.round(x0 + (w - _sideText.width) / 2);
         _sideText2.x = Math.round(x0 + (w - _sideText2.width) / 2);
         if(two)
         {
            centerY(_sideText,2,h * 0.55);
            centerY(_sideText2,h * 0.5,h * 0.5 - 3);
         }
         else
         {
            centerY(_sideText,0,h);
         }
         stopTweens(_side);
         _side.visible = true;
         var y:Number = sideY();
         if(_instant)
         {
            _side.y = y;
            _side.alpha = 1;
         }
         else
         {
            _side.y = y - 12;
            _side.alpha = 0;
            tween(_side,"y",y,0.3);
            tween(_side,"alpha",1,0.25);
         }
         _sideOpen = true;
         ++_sideGen;
         autoLater(SIDE_HOLD,"autoSide",[_sideGen]);
      }

      private function closeSide(immediate:Boolean = false) : void
      {
         if(!_sideOpen)
         {
            return;
         }
         _sideOpen = false;
         stopTweens(_side);
         if(immediate || _instant)
         {
            _side.visible = false;
            _side.alpha = 0;
         }
         else
         {
            tween(_side,"alpha",0,0.2,0,"hideSide");
            tween(_side,"y",_side.y - 10,0.2);
         }
      }

      // shootout: a marker per round in each team's shots box (gold tick = goal, red cross = miss, ring = to come)
      private function drawShootout() : void
      {
         var g:Graphics = _dots.graphics;
         g.clear();
         var n:int = _shootout != null && _shootout.numRounds != null ? int(_shootout.numRounds) : 3;
         var cur:int = shootoutRound();
         var show:int = Math.max(n,cur);
         var r:Number = 8;
         var step:Number = r * 2 + 8;
         var cellW:Number = CX - 2 - 52;
         var fitN:int = Math.max(1,int((cellW - 10) / step));
         var first:int = show > fitN ? show - fitN + 1 : 1;
         var i:int = 0;
         while(i < 2)
         {
            var side:String = i == 0 ? "away" : "home";
            var x0:Number = i == 0 ? 52 : BW - CX + 2;
            var cnt:int = show - first + 1;
            var k:int = first;
            while(k <= show)
            {
               var v:Number = _shootout != null ? Number(_shootout[side + "Round" + k]) : 0;
               var cx:Number = x0 + cellW / 2 + (k - first - (cnt - 1) / 2) * step;
               var cy:Number = ROW_Y + ROW_H / 2;
               if(v == 1)
               {
                  g.lineStyle();
                  g.beginFill(C_CLOCK);
                  g.drawCircle(cx,cy,r);
                  g.endFill();
                  g.lineStyle(2.2,C_BOX2);
                  g.moveTo(cx - r * 0.45,cy);
                  g.lineTo(cx - r * 0.1,cy + r * 0.4);
                  g.lineTo(cx + r * 0.5,cy - r * 0.4);
               }
               else if(v == 2)
               {
                  g.lineStyle();
                  g.beginFill(13238304);
                  g.drawCircle(cx,cy,r);
                  g.endFill();
                  g.lineStyle(2.2,16777215);
                  g.moveTo(cx - r * 0.4,cy - r * 0.4);
                  g.lineTo(cx + r * 0.4,cy + r * 0.4);
                  g.moveTo(cx + r * 0.4,cy - r * 0.4);
                  g.lineTo(cx - r * 0.4,cy + r * 0.4);
               }
               else
               {
                  g.lineStyle(2,C_RIM);
                  g.drawCircle(cx,cy,r - 1);
               }
               g.lineStyle();
               k++;
            }
            i++;
         }
      }

      private function pulseScore(i:int) : void
      {
         var t:TextField = _score[i];
         stopTweens(t);
         t.alpha = 0;
         later(0,"scoreIn",[i]);
         var fl:Shape = _flash[i];
         stopTweens(fl);
         fl.alpha = 0.45;
         tween(fl,"alpha",0,0.5);
      }

      private function scoreIn(i:int) : void
      {
         var t:TextField = _score[i];
         centerY(t,0,TOP);
         var y:Number = t.y;
         t.y = y - 14;
         tween(t,"alpha",1,0.3);
         tween(t,"y",y,0.3);
      }

      private function playSfx(id:String) : void
      {
         if(_audioCallBack != null)
         {
            try
            {
               _audioCallBack(id);
            }
            catch(e:Error)
            {
            }
         }
      }

      // ---------------------------------------------------------------- tween engine

      private function tween(target:Object, prop:String, to:Number, dur:Number, delay:Number = 0, done:String = null) : void
      {
         if(_instant)
         {
            target[prop] = to;
            if(done != null)
            {
               runAction(done,null);
            }
            return;
         }
         _tweens.push({
            "t":target,
            "p":prop,
            "to":to,
            "dur":Math.max(0.001,dur),
            "start":getTimer() + delay * 1000,
            "from":NaN,
            "done":done,
            "args":null
         });
      }

      private function later(delay:Number, action:String, args:Array) : void
      {
         if(_instant)
         {
            runAction(action,args);
            return;
         }
         _tweens.push({
            "t":null,
            "p":null,
            "to":0,
            "dur":0.001,
            "start":getTimer() + delay * 1000,
            "from":0,
            "done":action,
            "args":args
         });
      }

      private function autoLater(delay:Number, action:String, args:Array) : void
      {
         if(!_instant)
         {
            later(delay,action,args);
         }
      }

      private function runAction(action:String, args:Array) : void
      {
         if(action == "scores")
         {
            setScores(args[0],args[1]);
         }
         else if(action == "goal")
         {
            startGoal(Number(args[0]));
         }
         else if(action == "goalIn" && int(args[0]) == _goalGen && _goalSide >= 0)
         {
            goalIn();
         }
         else if(action == "gCover" && int(args[0]) == _goalGen && _goalUp)
         {
            goalCover();
         }
         else if(action == "gRing" && int(args[0]) == _goalGen && _goalUp)
         {
            _goalRing.alpha = 1;
            tween(_goalRing,"scaleX",3.2,0.55);
            tween(_goalRing,"scaleY",3.2,0.55);
            tween(_goalRing,"alpha",0,0.55);
         }
         else if(action == "gSpot" && int(args[0]) == _goalGen && _goalUp)
         {
            tween(_goalLogoBox,"x",_logoRestX,0.35);
            tween(_goalLogoBox,"scaleX",1,0.35);
            tween(_goalLogoBox,"scaleY",1,0.35);
         }
         else if(action == "gLetters" && int(args[0]) == _goalGen && _goalUp)

         {
            goalLetters();
         }
         else if(action == "gSweep" && int(args[0]) == _goalGen && _goalUp)
         {
            _goalSweep.x = -140;
            tween(_goalSweep,"x",BW + 20,0.6);
            later(1.6,"gSweep",[args[0]]);
         }
         else if(action == "gRebuild" && int(args[0]) == _goalGen && !_goalUp)
         {
            goalRebuild();
         }
         else if(action == "hideGoalNow" && int(args[0]) == _goalGen)
         {
            hideGoal();
         }
         else if(action == "afterGoal")
         {
            afterGoal();
         }
         else if(action == "scoreIn")
         {
            scoreIn(int(args[0]));
         }
         else if(action == "hideBug" && !_visible)
         {
            _bug.visible = false;
         }
         else if(action == "hideTab" && !_tabOpen)
         {
            _tab.visible = false;
         }
         else if(action == "hideSide" && !_sideOpen)
         {
            _side.visible = false;
         }
         else if(action == "autoSide" && int(args[0]) == _sideGen)
         {
            closeSide();
         }
         else if(action == "autoShots" && int(args[0]) == _shotsGen)
         {
            backToShots();
         }
         else if(action == "autoGoal" && int(args[0]) == _goalGen && _goalSide >= 0)
         {
            hideGoal();
         }
      }

      private function stopTweens(target:Object) : void
      {
         var keep:Array = [];
         for each(var tw:Object in _tweens)
         {
            if(tw.t != target)
            {
               keep.push(tw);
            }
         }
         _tweens = keep;
      }

      // the anchor is the bar's top-left corner, at (x, y) of a 1920x1080 design, whatever size the game renders at
      private function pin() : void
      {
         if(stage == null || _anchor == null)
         {
            return;
         }
         var sw:Number = stage.stageWidth > 0 ? stage.stageWidth : 1920;
         var sh:Number = stage.stageHeight > 0 ? stage.stageHeight : 1080;
         var target:Point = _zoom != 1 ? new Point(10 * sw / 1920,10 * sh / 1080) : new Point(X0 * sw / 1920,Y0 * sh / 1080);
         var local:Point = globalToLocal(target);
         _anchor.x = local.x;
         _anchor.y = local.y;
         var unit:Point = globalToLocal(new Point(target.x + sw / 1920,target.y + sh / 1080));
         var kx:Number = unit.x - local.x;
         var ky:Number = unit.y - local.y;
         if(kx > 0.01 && ky > 0.01)
         {
            _anchor.scaleX = kx * _zoom * SCALE;
            _anchor.scaleY = ky * _zoom * SCALE;
         }
      }

      private function onFrame(e:Event) : void
      {
         pin();
         for each(var fo:Object in _fits)
         {
            fitLogo(fo);
         }
         if(_tweens.length == 0)
         {
            return;
         }
         var now:int = getTimer();
         var keep:Array = [];
         var finished:Array = [];
         for each(var tw:Object in _tweens)
         {
            if(now < tw.start)
            {
               keep.push(tw);
            }
            else
            {
               if(tw.t != null)
               {
                  if(isNaN(tw.from))
                  {
                     tw.from = Number(tw.t[tw.p]);
                  }
                  var k:Number = Math.min(1,(now - tw.start) / (tw.dur * 1000));
                  var eased:Number = 1 - (1 - k) * (1 - k) * (1 - k);
                  tw.t[tw.p] = tw.from + (tw.to - tw.from) * eased;
                  if(k < 1)
                  {
                     keep.push(tw);
                     continue;
                  }
               }
               finished.push(tw);
            }
         }
         _tweens = keep;
         for each(var f:Object in finished)
         {
            if(f.done != null)
            {
               runAction(f.done,f.args);
            }
         }
      }
   }
}
