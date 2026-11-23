package
{
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
   import flash.net.URLRequest;
   import flash.text.AntiAliasType;
   import flash.text.TextField;
   import flash.text.TextFieldAutoSize;
   import flash.text.TextFormat;
   import flash.utils.getDefinitionByName;
   import flash.utils.getTimer;

   public class ScoreClock extends MovieClip
   {
      private static var X0:Number;
      private static var Y0:Number;
      private static var SCALE:Number;
      private static var ROW_H:Number;
      private static var SHOTS_H:Number;
      private static var H:Number;
      private static var GAP:Number;
      private static var RADIUS:Number;
      private static var SN_W:Number;
      private static var TEAM_W:Number;
      private static var CLOCK_W:Number;
      private static var EXTRA_W:Number;
      private static var TILE_W:Number;
      private static var TAB_H:Number;
      private static var LABEL_W:Number;
      private static var TEAM_X:Number;
      private static var TEAM_END:Number;
      private static var CLOCK_X:Number;
      private static var BAR_END:Number;
      private static var LOGO_TILE:Number;
      private static var GOAL_LOGO:Number;

      private static var C_GREY:uint;
      private static var C_GREY_DARK:uint;
      private static var C_NAVY:uint;
      private static var C_TEXT:uint;
      private static var C_BLUE:uint;
      private static var C_DIVIDER:uint;
      private static var C_SN:uint;
      private static var C_MISS:uint;
      private static var C_SIDE:uint;

      private static var S_ABBR:Number;
      private static var S_SCORE:Number;
      private static var S_CLOCK:Number;
      private static var S_PERIOD:Number;
      private static var S_SHOTS:Number;
      private static var S_SHOTS_LABEL:Number;
      private static var S_TAB:Number;
      private static var S_EXTRA:Number;
      private static var S_EXTRA_TIME:Number;
      private static var S_SIDE:Number;
      private static var S_GOAL:Number;
      private static var S_GOAL_PP:Number;
      private static var S_NAME:Number;
      private static var S_EMBOSS:Number;
      private static var NAME_SPACING:Number;
      private static var GOAL_STEP:Number;
      private static var GOAL_SKEW:Number;

      private static var SHOW_SN:Boolean;
      private static var DARKEN_LIGHT:Boolean;
      private static var PP_LOGO:Boolean;
      private static var GOAL_BANNER:Boolean;
      private static var SHOTS_ROW:Boolean;
      private static var GOAL_STREAKS:Boolean;

      private static var L_PP:String;
      private static var L_GOAL:String;
      private static var L_SHOOTOUT:String;
      private static var L_ROUND:String;
      private static var L_SHOTS:String;
      private static var L_FINAL:String;

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
__CFG__
      private static var _configured:Boolean = false;
      private static const AUDIO_GOAL_AWAY:String = "scoreclock_goalscored_show_away";
      private static const AUDIO_GOAL_HOME:String = "scoreclock_goalscored_show_home";
      private static const AUDIO_PENALTY_AWAY:String = "scoreclock_penalty_show_away";
      private static const AUDIO_PENALTY_HOME:String = "scoreclock_penalty_show_home";
      private static const AUDIO_HIGHLIGHT_AWAY:String = "scoreclock_player_highlight_show_away";
      private static const AUDIO_HIGHLIGHT_HOME:String = "scoreclock_player_highlight_show_home";
      private static const TIMED_GOAL_HOLD:Number = 3;
      private static const DEFAULT_NAMES:String = "ANA:DUCKS|ARI:COYOTES|UTA:UTAH|BOS:BRUINS|BUF:SABRES|CGY:FLAMES|CAR:HURRICANES|CHI:BLACKHAWKS|COL:AVALANCHE|CBJ:BLUE JACKETS|DAL:STARS|DET:RED WINGS|EDM:OILERS|FLA:PANTHERS|LA:KINGS|LAK:KINGS|MIN:WILD|MTL:CANADIENS|MON:CANADIENS|NSH:PREDATORS|NJ:DEVILS|NJD:DEVILS|NYI:ISLANDERS|NYR:RANGERS|OTT:SENATORS|PHI:FLYERS|PIT:PENGUINS|SJ:SHARKS|SJS:SHARKS|SEA:KRAKEN|STL:BLUES|TB:LIGHTNING|TBL:LIGHTNING|TOR:MAPLE LEAFS|VAN:CANUCKS|VGK:GOLDEN KNIGHTS|VEG:GOLDEN KNIGHTS|WSH:CAPITALS|WAS:CAPITALS|WPG:JETS|WIN:JETS";
      public static const SN_ART:Array = __SN_ART__;

      private var _anchor:Sprite;
      private var _zoom:Number = 1;
      private var _bug:Sprite;
      private var _main:Sprite;
      private var _sn:Sprite;
      private var _rows:Array = [];
      private var _rowBg:Array = [];
      private var _abbr:Array = [];
      private var _score:Array = [];
      private var _flash:Array = [];
      private var _shotsRow:Sprite;
      private var _shotsVal:Array = [];
      private var _shotsLabel:TextField;
      private var _shootoutDots:Shape;
      private var _shotsDiv:Shape;
      private var _soDiv:Shape;
      private var _clock:Sprite;
      private var _clockBg:Shape;
      private var _clockText:TextField;
      private var _periodText:TextField;
      private var _practiceText:TextField;
      private var _clockDivider:Shape;
      private var _tile:Sprite;
      private var _tileBg:Shape;
      private var _tileLogo:Array = [];
      private var _tileMask:Shape;
      private var _tileOpen:Boolean = false;
      private var _tab:Sprite;
      private var _tabBg:Shape;
      private var _tabLabel:TextField;
      private var _tabTime:TextField;
      private var _tabOpen:Boolean = false;
      private var _extra:Sprite;
      private var _extraBg:Shape;
      private var _extraLabel:TextField;
      private var _extraTime:TextField;
      private var _extraOpen:Boolean = false;
      private var _side:Sprite;
      private var _sideBg:Shape;
      private var _sideText:TextField;
      private var _sideText2:TextField;
      private var _sideMask:Shape;
      private var _sideOpen:Boolean = false;
      private var _sideGen:int = 0;
      private var _shotsGen:int = 0;
      private var _goalGen:int = 0;
      private var _goal:Sprite;
      private var _goalBlue:Shape;
      private var _goalBlueMask:Shape;
      private var _streaks:Array = [];
      private var _goalTeam:Sprite;
      private var _goalTeamBg:Shape;
      private var _goalTeamMask:Shape;
      private var _emboss:TextField;
      private var _goalLogoBox:Sprite;
      private var _goalLogoHolder:Array = [];
      private var _nameBox:Sprite;
      private var _watermark:Sprite;
      private var _wmHolder:Array = [];
      private var _nameSlices:Array = [];
      private var _nameMasks:Array = [];
      private var _underline:Shape;
      private var _goalLetters:Array = [];
      private var _goalPP:TextField;
      private var _goalUp:Boolean = false;
      private var _goalPending:Boolean = false;
      private var _goalStart:int = 0;
      private var _goalIsPP:Boolean = false;
      private var _covered:Boolean = false;
      private var _pendingPulse:Array = [false,false];
      private var _pendingSide:Array = null;
      private var _primary:Array = [1981560,9051178];
      private var _secondary:Array = [2829099,2829099];
      private var _names:Array = ["AWAY","HOME"];
      private var _scores:Array = ["0","0"];
      private var _shots:Array = ["",""];
      private var _hasShots:Boolean = false;
      private var _final:Boolean = false;
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
      private var _clockShift:Number = 0;

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
         X0 = cfgNum(c,"x",48);
         Y0 = cfgNum(c,"y",38);
         SCALE = Math.max(0.3,Math.min(3,cfgNum(c,"scale",1)));
         ROW_H = cfgNum(c,"rowH",52);
         SHOTS_H = cfgNum(c,"shotsH",30);
         GAP = cfgNum(c,"gap",2);
         RADIUS = cfgNum(c,"radius",8);
         SN_W = cfgNum(c,"snW",84);
         TEAM_W = cfgNum(c,"teamW",134);
         CLOCK_W = cfgNum(c,"clockW",92);
         EXTRA_W = cfgNum(c,"extraW",86);
         TILE_W = cfgNum(c,"tileW",56);
         TAB_H = cfgNum(c,"tabH",30);
         LABEL_W = cfgNum(c,"labelW",116);
         LOGO_TILE = cfgNum(c,"tileLogo",58);
         GOAL_LOGO = cfgNum(c,"goalLogo",76);
         H = ROW_H + SHOTS_H;
         TEAM_X = SN_W + GAP;
         TEAM_END = TEAM_X + TEAM_W * 2;
         CLOCK_X = TEAM_END + GAP;
         BAR_END = CLOCK_X + CLOCK_W;
         C_GREY = cfgNum(c,"cGrey",15461100);
         C_GREY_DARK = cfgNum(c,"cGreyDark",14278117);
         C_NAVY = cfgNum(c,"cNavy",1846074);
         C_TEXT = cfgNum(c,"cText",16777215);
         C_BLUE = cfgNum(c,"cGoalBlue",1991624);
         C_DIVIDER = cfgNum(c,"cDivider",11581885);
         C_SN = cfgNum(c,"cSnBox",15855858);
         C_MISS = cfgNum(c,"cMiss",10134445);
         C_SIDE = cfgNum(c,"cSide",1846074);
         S_ABBR = cfgNum(c,"sAbbr",30);
         S_SCORE = cfgNum(c,"sScore",40);
         S_CLOCK = cfgNum(c,"sClock",29);
         S_PERIOD = cfgNum(c,"sPeriod",22);
         S_SHOTS = cfgNum(c,"sShots",22);
         S_SHOTS_LABEL = cfgNum(c,"sShotsLabel",20);
         S_TAB = cfgNum(c,"sTab",20);
         S_EXTRA = cfgNum(c,"sExtra",18);
         S_EXTRA_TIME = cfgNum(c,"sExtraTime",26);
         S_SIDE = cfgNum(c,"sSide",22);
         S_GOAL = cfgNum(c,"sGoal",64);
         S_GOAL_PP = cfgNum(c,"sGoalPP",16);
         S_NAME = cfgNum(c,"sName",40);
         S_EMBOSS = cfgNum(c,"sEmboss",74);
         NAME_SPACING = cfgNum(c,"nameSpacing",14);
         GOAL_STEP = cfgNum(c,"goalLetterStep",50);
         GOAL_SKEW = cfgNum(c,"goalSkew",0.2);
         SHOW_SN = cfgFlag(c,"showSn",true);
         DARKEN_LIGHT = cfgFlag(c,"darkenLight",true);
         PP_LOGO = cfgFlag(c,"ppLogo",true);
         GOAL_BANNER = cfgFlag(c,"goalBanner",true);
         SHOTS_ROW = cfgFlag(c,"shotsRow",true);
         GOAL_STREAKS = cfgFlag(c,"goalStreaks",true);
         L_PP = cfgStr(c,"lPowerPlay","POWER PLAY");
         L_GOAL = cfgStr(c,"lGoal","GOAL");
         L_SHOOTOUT = cfgStr(c,"lShootout","SHOOTOUT");
         L_ROUND = cfgStr(c,"lRound","ROUND");
         L_SHOTS = cfgStr(c,"lShots","SHOTS");
         L_FINAL = cfgStr(c,"lFinal","FINAL");
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

      private static function drawArt(g:Graphics, data:Array, sx:Number, sy:Number, ox:Number, oy:Number) : void
      {
         for each(var p:Array in data)
         {
            g.beginFill(uint(p[0]));
            var c:Array = p[2];
            var k:int = 0;
            while(k < c.length)
            {
               var op:int = int(c[k]);
               if(op == 1)
               {
                  g.moveTo(c[k + 1] * sx + ox,c[k + 2] * sy + oy);
                  k += 3;
               }
               else if(op == 2)
               {
                  g.lineTo(c[k + 1] * sx + ox,c[k + 2] * sy + oy);
                  k += 3;
               }
               else
               {
                  g.curveTo(c[k + 1] * sx + ox,c[k + 2] * sy + oy,c[k + 3] * sx + ox,c[k + 4] * sy + oy);
                  k += 5;
               }
            }
            g.endFill();
         }
      }

      private static function centerY(t:TextField, top:Number, h:Number) : void
      {
         t.y = Math.round(top + (h - t.height) / 2 + t.height * 0.02);
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

      private static function rowColour(c:uint) : uint
      {
         if(!DARKEN_LIGHT)
         {
            return c;
         }
         var l:Number = lum(c);
         return l > 0.62 ? mix(c,0,Math.min(0.55,(l - 0.62) * 1.6 + 0.25)) : c;
      }

      private static function setFieldText(t:TextField, s:String, color:uint) : void
      {
         var f:TextFormat = t.defaultTextFormat;
         f.color = color;
         t.defaultTextFormat = f;
         t.text = s == null || s == "" ? " " : s;
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

      private static function roundRect(g:Graphics, x:Number, y:Number, w:Number, h:Number, rTR:Number, rBR:Number) : void
      {
         g.moveTo(x,y);
         g.lineTo(x + w - rTR,y);
         if(rTR > 0)
         {
            g.curveTo(x + w,y,x + w,y + rTR);
         }
         g.lineTo(x + w,y + h - rBR);
         if(rBR > 0)
         {
            g.curveTo(x + w,y + h,x + w - rBR,y + h);
         }
         g.lineTo(x,y + h);
         g.lineTo(x,y);
      }

      private static function greyFill(g:Graphics, w:Number, h:Number, y:Number) : void
      {
         var m:Matrix = new Matrix();
         m.createGradientBox(w,h,Math.PI / 2,0,y);
         g.beginGradientFill(GradientType.LINEAR,[mix(C_GREY,16777215,0.45),C_GREY,C_GREY_DARK],[1,1,1],[0,110,255],m);
      }

      private function teamPri(i:int) : uint
      {
         var o:Array = TEAM_COLOURS[String(_names[i]).toUpperCase()];
         return o != null ? uint(o[0]) : uint(_primary[i]);
      }

      private function teamCol(i:int) : uint
      {
         return rowColour(teamPri(i));
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

      private function rebuild() : void
      {
         _tweens = [];
         _anchor.removeChild(_bug);
         _bug = new Sprite();
         _bug.alpha = 0;
         _bug.visible = false;
         _anchor.addChild(_bug);
         _rows = [];
         _rowBg = [];
         _abbr = [];
         _score = [];
         _flash = [];
         _shotsVal = [];
         _tileLogo = [];
         _streaks = [];
         _goalLogoHolder = [];
         _wmHolder = [];
         _nameSlices = [];
         _nameMasks = [];
         _goalLetters = [];
         _tabOpen = _extraOpen = _sideOpen = _tileOpen = _goalUp = _goalPending = _covered = false;
         _clockShift = 0;
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
            "teamColorAway":uint(p.awayColor || "0xF47A38"),
            "teamSecondaryColorAway":uint(p.awaySecondary || p.awayColor || "0xF47A38"),
            "teamColorHome":uint(p.homeColor || "0xC8102E"),
            "teamSecondaryColorHome":uint(p.homeSecondary || p.homeColor || "0xC8102E"),
            "logoPathAway":p.awayLogo,
            "logoPathHome":p.homeLogo
         },null,null);
         setAwayTeamName(p.awayName != null ? p.awayName : "ANA");
         setHomeTeamName(p.homeName != null ? p.homeName : "CGY");
         setScores([p.awayScore != null ? p.awayScore : "0",p.homeScore != null ? p.homeScore : "1"],false);
         setPeriod(p.period != null ? p.period : "1ST");
         setTime(p.clock != null ? p.clock : "17:37");
         showClock(true);
         showComparison([p.awayShots || "5",p.homeShots || "10","SHOTS"]);
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
               showGoalWithDelay(HOME,0.5);
            }
            if(st == "goalnow")
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
         _tab = new Sprite();
         _tabBg = new Shape();
         _tab.addChild(_tabBg);
         _tabLabel = text(FONT,S_TAB,C_TEXT);
         _tabTime = text(FONT,S_TAB,C_TEXT);
         _tab.addChild(_tabLabel);
         _tab.addChild(_tabTime);
         _tab.y = H - TAB_H;
         _tab.visible = false;
         _bug.addChild(_tab);
         _extra = new Sprite();
         _extraBg = new Shape();
         _extra.addChild(_extraBg);
         _extraLabel = text(FONT,S_EXTRA,C_NAVY);
         _extraTime = text(FONT,S_EXTRA_TIME,C_NAVY);
         _extra.addChild(_extraLabel);
         _extra.addChild(_extraTime);
         var eg:Graphics = _extraBg.graphics;
         greyFill(eg,EXTRA_W,H,0);
         roundRect(eg,0,0,EXTRA_W,H,RADIUS * 0.5,RADIUS);
         eg.endFill();
         _extra.x = BAR_END + GAP;
         _extra.visible = false;
         _extra.alpha = 0;
         _bug.addChild(_extra);
         _side = new Sprite();
         _sideBg = new Shape();
         _side.addChild(_sideBg);
         _sideText = text(FONT,S_SIDE,C_TEXT);
         _sideText2 = text(FONT,S_SIDE * 0.8,C_TEXT);
         _side.addChild(_sideText);
         _side.addChild(_sideText2);
         _sideMask = new Shape();
         _side.mask = _sideMask;
         _side.visible = false;
         _bug.addChild(_side);
         _bug.addChild(_sideMask);
         _main = new Sprite();
         _bug.addChild(_main);
         var i:int = 0;
         while(i < 2)
         {
            buildRow(i);
            i++;
         }
         buildShots();
         buildTile();
         buildClock();
         buildGoal();
         _sn = new Sprite();
         buildSn();
         _bug.addChild(_sn);
         repaintRows();
      }

      private function buildSn() : void
      {
         var g:Graphics = _sn.graphics;
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(SN_W,H,Math.PI / 2,0,0);
         g.beginGradientFill(GradientType.LINEAR,[16777215,C_SN,mix(C_SN,0,0.07)],[1,1,1],[0,120,255],m);
         g.drawRect(0,0,SN_W,H);
         g.endFill();
         if(SHOW_SN)
         {
            var w:Number = SN_W - 18;
            var s:Number = w / 290;
            drawArt(g,SN_ART,s,s,(SN_W - w) / 2 + 1,(H - 163 * s) / 2);
         }
      }

      private function buildRow(i:int) : void
      {
         var row:Sprite = new Sprite();
         row.x = TEAM_X + i * TEAM_W;
         var bg:Shape = new Shape();
         row.addChild(bg);
         var ab:TextField = text(FONT,S_ABBR,C_TEXT);
         var sc:TextField = text(FONT_HEAVY,S_SCORE,C_TEXT);
         row.addChild(ab);
         row.addChild(sc);
         var fl:Shape = new Shape();
         fl.graphics.beginFill(C_TEXT);
         fl.graphics.drawRect(0,0,TEAM_W,ROW_H);
         fl.graphics.endFill();
         fl.alpha = 0;
         row.addChild(fl);
         _main.addChild(row);
         _rows[i] = row;
         _rowBg[i] = bg;
         _abbr[i] = ab;
         _score[i] = sc;
         _flash[i] = fl;
      }

      private function buildShots() : void
      {
         _shotsRow = new Sprite();
         _shotsRow.x = TEAM_X;
         _shotsRow.y = ROW_H;
         var g:Graphics = _shotsRow.graphics;
         var w:Number = TEAM_W * 2;
         greyFill(g,w,SHOTS_H,0);
         g.drawRect(0,0,w,SHOTS_H);
         g.endFill();
         var cell:Number = (w - LABEL_W) / 2;
         _shotsDiv = new Shape();
         _shotsDiv.graphics.beginFill(C_DIVIDER);
         _shotsDiv.graphics.drawRect(Math.round(cell) - 1,6,2,SHOTS_H - 12);
         _shotsDiv.graphics.drawRect(Math.round(w - cell) - 1,6,2,SHOTS_H - 12);
         _shotsDiv.graphics.endFill();
         _shotsRow.addChild(_shotsDiv);
         _soDiv = new Shape();
         _soDiv.graphics.beginFill(C_DIVIDER);
         _soDiv.graphics.drawRect(Math.round(w / 2) - 1,6,2,SHOTS_H - 12);
         _soDiv.graphics.endFill();
         _soDiv.visible = false;
         _shotsRow.addChild(_soDiv);
         var i:int = 0;
         while(i < 2)
         {
            var t:TextField = text(FONT,S_SHOTS,C_NAVY);
            _shotsRow.addChild(t);
            _shotsVal[i] = t;
            i++;
         }
         _shotsLabel = text(FONT,S_SHOTS_LABEL,C_NAVY);
         setFieldText(_shotsLabel,L_SHOTS,C_NAVY);
         _shotsRow.addChild(_shotsLabel);
         _shootoutDots = new Shape();
         _shotsRow.addChild(_shootoutDots);
         _shotsRow.visible = SHOTS_ROW;
         _main.addChild(_shotsRow);
         layoutShots();
      }

      private function buildTile() : void
      {
         _tile = new Sprite();
         _tile.x = TEAM_END;
         _tileBg = new Shape();
         _tile.addChild(_tileBg);
         var i:int = 0;
         while(i < 2)
         {
            var h:Sprite = new Sprite();
            h.scaleX = h.scaleY = LOGO_TILE / 256;
            h.x = Math.round(GAP + (TILE_W - LOGO_TILE) / 2);
            h.y = Math.round((H - LOGO_TILE) / 2);
            h.visible = false;
            _tile.addChild(h);
            _tileLogo[i] = h;
            i++;
         }
         _tileMask = new Shape();
         _tileMask.graphics.beginFill(0);
         _tileMask.graphics.drawRect(0,0,TILE_W + GAP,H);
         _tileMask.graphics.endFill();
         _tileMask.x = TEAM_END;
         _tileMask.scaleX = 0;
         _tile.mask = _tileMask;
         _tile.visible = false;
         _main.addChild(_tile);
         _main.addChild(_tileMask);
      }

      private function buildClock() : void
      {
         _clock = new Sprite();
         _clock.x = CLOCK_X;
         _clockBg = new Shape();
         var g:Graphics = _clockBg.graphics;
         greyFill(g,CLOCK_W,H,0);
         roundRect(g,0,0,CLOCK_W,H,RADIUS * 0.5,RADIUS);
         g.endFill();
         _clock.addChild(_clockBg);
         _clockDivider = new Shape();
         _clockDivider.graphics.beginFill(C_DIVIDER);
         _clockDivider.graphics.drawRect(10,ROW_H - 1,CLOCK_W - 20,2);
         _clockDivider.graphics.endFill();
         _clock.addChild(_clockDivider);
         _clockText = text(FONT,S_CLOCK,C_NAVY);
         _periodText = text(FONT,S_PERIOD,C_NAVY);
         _practiceText = text(FONT,17,C_NAVY);
         _practiceText.visible = false;
         _clock.addChild(_clockText);
         _clock.addChild(_periodText);
         _clock.addChild(_practiceText);
         _main.addChild(_clock);
         setTime("20:00");
         setPeriod("1ST");
      }

      private function buildGoal() : void
      {
         var W:Number = BAR_END - TEAM_X;
         _goal = new Sprite();
         _goal.x = TEAM_X;
         _goal.visible = false;
         _goalBlue = new Shape();
         var g:Graphics = _goalBlue.graphics;
         var m:Matrix = new Matrix();
         m.createGradientBox(W,H,Math.PI / 2,0,0);
         g.beginGradientFill(GradientType.LINEAR,[mix(C_BLUE,16777215,0.12),C_BLUE,mix(C_BLUE,0,0.18)],[1,1,1],[0,120,255],m);
         roundRect(g,0,0,W,H,RADIUS * 0.5,RADIUS);
         g.endFill();
         _goal.addChild(_goalBlue);
         var i:int = 0;
         while(i < 2)
         {
            var sw:Number = i == 0 ? W * 0.32 : W * 0.14;
            var st:Shape = new Shape();
            st.graphics.beginFill(16777215,i == 0 ? 0.95 : 0.85);
            st.graphics.drawRect(0,0,sw,H);
            st.graphics.endFill();
            st.x = -sw;
            st.visible = GOAL_STREAKS;
            _goal.addChild(st);
            _streaks[i] = st;
            i++;
         }
         _goalBlueMask = new Shape();
         _goalBlueMask.graphics.beginFill(0);
         _goalBlueMask.graphics.drawRect(0,0,W,H);
         _goalBlueMask.graphics.endFill();
         _goalBlueMask.scaleX = 0;
         _goal.addChild(_goalBlueMask);
         var blueLayer:Sprite = new Sprite();
         _goal.removeChild(_goalBlue);
         _goal.removeChild(Shape(_streaks[0]));
         _goal.removeChild(Shape(_streaks[1]));
         blueLayer.addChild(_goalBlue);
         blueLayer.addChild(Shape(_streaks[0]));
         blueLayer.addChild(Shape(_streaks[1]));
         blueLayer.mask = _goalBlueMask;
         _goal.addChildAt(blueLayer,0);
         _goalTeam = new Sprite();
         _goalTeamBg = new Shape();
         _goalTeam.addChild(_goalTeamBg);
         _emboss = text(FONT_HEAVY,S_EMBOSS,C_TEXT);
         _goalTeam.addChild(_emboss);
         _goalLogoBox = new Sprite();
         _goalLogoBox.x = Math.round(W * 0.43);
         _goalLogoBox.y = Math.round(H / 2);
         i = 0;
         while(i < 2)
         {
            var gh:Sprite = new Sprite();
            gh.scaleX = gh.scaleY = GOAL_LOGO / 256;
            gh.x = -GOAL_LOGO / 2;
            gh.y = -GOAL_LOGO / 2;
            gh.visible = false;
            _goalLogoBox.addChild(gh);
            _goalLogoHolder[i] = gh;
            i++;
         }
         _goalTeam.addChild(_goalLogoBox);
         _nameBox = new Sprite();
         _watermark = new Sprite();
         _watermark.x = Math.round(W / 2);
         _watermark.y = Math.round(H / 2);
         i = 0;
         while(i < 2)
         {
            var wh:Sprite = new Sprite();
            var wsz:Number = H * 2.2;
            wh.scaleX = wh.scaleY = wsz / 256;
            wh.x = -wsz / 2;
            wh.y = -wsz / 2;
            wh.visible = false;
            _watermark.addChild(wh);
            _wmHolder[i] = wh;
            i++;
         }
         _watermark.alpha = 0.13;
         _nameBox.addChild(_watermark);
         i = 0;
         while(i < 3)
         {
            var nt:TextField = text(FONT,S_NAME,C_TEXT);
            var tf:TextFormat = nt.defaultTextFormat;
            tf.letterSpacing = NAME_SPACING;
            nt.defaultTextFormat = tf;
            _nameBox.addChild(nt);
            _nameSlices[i] = nt;
            var nm:Shape = new Shape();
            nm.graphics.beginFill(0);
            nm.graphics.drawRect(0,i * H / 3,W,H / 3 + 0.5);
            nm.graphics.endFill();
            _nameBox.addChild(nm);
            nt.mask = nm;
            _nameMasks[i] = nm;
            i++;
         }
         _underline = new Shape();
         _nameBox.addChild(_underline);
         _nameBox.visible = false;
         _goalTeam.addChild(_nameBox);
         var word:String = L_GOAL;
         i = 0;
         while(i < word.length)
         {
            var t:TextField = text(FONT_HEAVY,S_GOAL,C_TEXT);
            setFieldText(t,word.charAt(i),C_TEXT);
            t.filters = [new DropShadowFilter(2,90,0,0.45,4,4,1)];
            var ls:Sprite = new Sprite();
            ls.addChild(t);
            t.x = -t.width / 2;
            t.y = -t.height / 2;
            var lm:Matrix = new Matrix(1,0,-GOAL_SKEW,1,Math.round(W / 2 + (i - (word.length - 1) / 2) * GOAL_STEP),Math.round(H / 2 + 2));
            ls.transform.matrix = lm;
            ls.visible = false;
            _goalTeam.addChild(ls);
            _goalLetters[i] = ls;
            i++;
         }
         _goalPP = text(FONT,S_GOAL_PP,C_TEXT);
         _goalPP.visible = false;
         _goalTeam.addChild(_goalPP);
         _goalTeamMask = new Shape();
         _goalTeamMask.graphics.beginFill(0);
         roundRect(_goalTeamMask.graphics,0,0,W,H,RADIUS * 0.5,RADIUS);
         _goalTeamMask.graphics.endFill();
         _goalTeamMask.scaleX = 0;
         _goal.addChild(_goalTeam);
         _goal.addChild(_goalTeamMask);
         _goalTeam.mask = _goalTeamMask;
         _bug.addChild(_goal);
      }

      private function paintGoal(i:int) : void
      {
         var W:Number = BAR_END - TEAM_X;
         var c:uint = teamCol(i);
         var g:Graphics = _goalTeamBg.graphics;
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(W,H,Math.PI / 2,0,0);
         g.beginGradientFill(GradientType.LINEAR,[mix(c,16777215,0.1),c,mix(c,0,0.2)],[1,1,1],[0,120,255],m);
         roundRect(g,0,0,W,H,RADIUS * 0.5,RADIUS);
         g.endFill();
         var name:String = nickname(i);
         var tiled:String = name + " " + name + " " + name + " " + name;
         setFieldText(_emboss,tiled,mix(c,0,0.06));
         var ef:TextFormat = _emboss.defaultTextFormat;
         ef.letterSpacing = 4;
         _emboss.defaultTextFormat = ef;
         _emboss.setTextFormat(ef);
         _emboss.filters = [new DropShadowFilter(2,225,16777215,0.32,2,2,1),new DropShadowFilter(3,45,0,0.55,3,3,1)];
         _emboss.x = -Math.round(_emboss.width * 0.12);
         centerY(_emboss,0,H);
         _emboss.alpha = 1;
         var k:int = 0;
         while(k < 3)
         {
            var nt:TextField = _nameSlices[k];
            var f:TextFormat = nt.defaultTextFormat;
            f.letterSpacing = NAME_SPACING;
            nt.defaultTextFormat = f;
            setFieldText(nt,name,C_TEXT);
            fit(nt,W - 40);
            nt.x = Math.round((W - nt.width) / 2);
            centerY(nt,0,H);
            nt.alpha = 1;
            k++;
         }
         var ug:Graphics = _underline.graphics;
         ug.clear();
         ug.beginFill(C_TEXT,0.9);
         var nt0:TextField = _nameSlices[0];
         ug.drawRect(Math.round(nt0.x + nt0.width * 0.55),Math.round(nt0.y + nt0.height - 4),Math.round(nt0.width * 0.45),2);
         ug.endFill();
         _underline.alpha = 1;
         k = 0;
         while(k < 2)
         {
            Sprite(_goalLogoHolder[k]).visible = k == i;
            Sprite(_wmHolder[k]).visible = k == i;
            k++;
         }
         setFieldText(_goalPP,L_PP,C_TEXT);
         _goalPP.x = Math.round((W - _goalPP.width) / 2);
         _goalPP.y = 2;
      }

      private function repaintRows() : void
      {
         var i:int = 0;
         while(i < 2)
         {
            var c:uint = teamCol(i);
            var g:Graphics = Shape(_rowBg[i]).graphics;
            g.clear();
            var m:Matrix = new Matrix();
            m.createGradientBox(TEAM_W,ROW_H,Math.PI / 2,0,0);
            g.beginGradientFill(GradientType.LINEAR,[mix(c,16777215,0.1),c,mix(c,0,0.14)],[1,1,1],[0,110,255],m);
            g.drawRect(0,0,TEAM_W,ROW_H);
            g.endFill();
            i++;
         }
         paintTile();
         layoutRows();
      }

      private function layoutRows() : void
      {
         var i:int = 0;
         while(i < 2)
         {
            var ab:TextField = _abbr[i];
            setFieldText(ab,String(_names[i]).toUpperCase(),C_TEXT);
            fit(ab,TEAM_W * 0.55);
            ab.x = 14;
            centerY(ab,0,ROW_H);
            var sc:TextField = _score[i];
            setFieldText(sc,_scores[i],C_TEXT);
            fit(sc,TEAM_W * 0.36);
            sc.x = Math.round(TEAM_W - 12 - sc.width);
            centerY(sc,0,ROW_H);
            i++;
         }
      }

      private function layoutShots() : void
      {
         var w:Number = TEAM_W * 2;
         var cell:Number = (w - LABEL_W) / 2;
         var i:int = 0;
         while(i < 2)
         {
            var t:TextField = _shotsVal[i];
            setFieldText(t,_shots[i],C_NAVY);
            t.visible = !_isShootout;
            t.x = Math.round((i == 0 ? 0 : w - cell) + (cell - t.width) / 2);
            centerY(t,0,SHOTS_H);
            i++;
         }
         _shotsLabel.visible = !_isShootout;
         fit(_shotsLabel,LABEL_W - 10);
         _shotsLabel.x = Math.round((w - _shotsLabel.width) / 2);
         centerY(_shotsLabel,0,SHOTS_H);
         _shootoutDots.visible = _isShootout;
         _shotsDiv.visible = !_isShootout;
         _soDiv.visible = _isShootout;
      }

      private function paintTile() : void
      {
         if(_tileBg == null)
         {
            return;
         }
         var i:int = sideIndex(_warnSide);
         var c:uint = i >= 0 ? teamCol(i) : C_NAVY;
         var g:Graphics = _tileBg.graphics;
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(TILE_W,H,Math.PI / 2,0,0);
         g.beginGradientFill(GradientType.LINEAR,[mix(c,16777215,0.1),c,mix(c,0,0.14)],[1,1,1],[0,110,255],m);
         g.drawRect(GAP,0,TILE_W,H);
         g.endFill();
         var k:int = 0;
         while(k < 2)
         {
            Sprite(_tileLogo[k]).visible = k == i;
            k++;
         }
      }

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
         repaintRows();
         loadLogo(_tileLogo[0],param1.logoPathAway);
         loadLogo(_tileLogo[1],param1.logoPathHome);
         loadLogo(_goalLogoHolder[0],param1.logoPathAway);
         loadLogo(_goalLogoHolder[1],param1.logoPathHome);
         loadLogo(_wmHolder[0],param1.logoPathAway);
         loadLogo(_wmHolder[1],param1.logoPathHome);
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
         clip = new MovieClip();
         holder.addChild(clip);
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
         repaintRows();
      }

      public function setHomeTeamName(param1:String) : void
      {
         _names[1] = param1;
         repaintRows();
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
         layoutRows();
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

      public function setPeriod(param1:String) : void
      {
         var s:String = param1 == null ? "" : trim(param1).toUpperCase();
         _final = s.indexOf("FINAL") >= 0;
         setFieldText(_periodText,_final && s == "FINAL" ? L_FINAL : s,C_NAVY);
         layoutClock();
      }

      public function setTime(param1:String) : void
      {
         setFieldText(_clockText,param1,C_NAVY);
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
         _clockDivider.visible = !_final && !practice;
         _periodText.visible = !practice;
         fit(_clockText,CLOCK_W - 12);
         _clockText.x = Math.round((CLOCK_W - _clockText.width) / 2);
         centerY(_clockText,0,ROW_H);
         if(_final)
         {
            _periodText.scaleX = _periodText.scaleY = 1;
            var fs:Number = Math.min(S_CLOCK / S_PERIOD,(CLOCK_W - 12) / Math.max(1,_periodText.width));
            _periodText.scaleX = _periodText.scaleY = fs;
            _periodText.x = Math.round((CLOCK_W - _periodText.width) / 2);
            centerY(_periodText,0,H);
         }
         else
         {
            fit(_periodText,CLOCK_W - 12);
            _periodText.x = Math.round((CLOCK_W - _periodText.width) / 2);
            centerY(_periodText,ROW_H,SHOTS_H);
         }
      }

      public function setPracticeText(param1:String) : void
      {
         var on:Boolean = param1 != null && param1 != "";
         _practiceText.visible = on;
         if(on)
         {
            setFieldText(_practiceText,param1,C_NAVY);
            fit(_practiceText,CLOCK_W - 10);
            _practiceText.x = Math.round((CLOCK_W - _practiceText.width) / 2);
            _practiceText.y = Math.round((H - _practiceText.height) / 2);
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
            _bug.x = 0;
         }
         else
         {
            if(_bug.alpha < 0.01)
            {
               _bug.x = -30;
            }
            tween(_bug,"alpha",1,0.3);
            tween(_bug,"x",0,0.35);
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
            tween(_bug,"x",-30,0.25);
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
         var tabUp:Boolean = _tabOpen || _extraOpen;
         refreshBar();
         closeSide(true);
         _goalPending = true;
         _goalStart = getTimer();
         if(!GOAL_BANNER)
         {
            var fl:Shape = _flash[i];
            stopTweens(fl);
            fl.alpha = 0;
            var k:int = 0;
            while(k < 3 && !_instant)
            {
               tween(fl,"alpha",0.55,0.12,k * 0.45);
               tween(fl,"alpha",0,0.28,k * 0.45 + 0.12);
               k++;
            }
            _goalPending = false;
            autoLater(2,"autoGoal",[_goalGen]);
            return;
         }
         if(_instant)
         {
            goalIn();
            goalSnapHeld();
            return;
         }
         later(tabUp ? 0.3 : 0.05,"goalIn",[_goalGen]);
         autoLater(GOAL_MAX_HOLD,"autoGoal",[_goalGen]);
      }

      private function goalReset() : void
      {
         var W:Number = BAR_END - TEAM_X;
         stopTweens(_goalBlueMask);
         stopTweens(_goalTeamMask);
         stopTweens(_emboss);
         stopTweens(_goalLogoBox);
         stopTweens(_nameBox);
         stopTweens(_underline);
         stopTweens(_goalPP);
         _goalBlueMask.scaleX = 0;
         _goalBlue.visible = true;
         _goalTeamMask.scaleX = 0;
         var k:int = 0;
         while(k < 2)
         {
            var st:Shape = _streaks[k];
            stopTweens(st);
            st.x = -st.width;
            k++;
         }
         _goalLogoBox.scaleX = _goalLogoBox.scaleY = 0.85;
         _goalLogoBox.alpha = 1;
         _goalLogoBox.visible = true;
         _nameBox.visible = false;
         _nameBox.alpha = 1;
         k = 0;
         while(k < 3)
         {
            var nt:TextField = _nameSlices[k];
            stopTweens(nt);
            nt.alpha = 1;
            nt.x = Math.round((W - nt.width) / 2);
            k++;
         }
         _underline.alpha = 1;
         k = 0;
         while(k < _goalLetters.length)
         {
            var ls:Sprite = _goalLetters[k];
            stopTweens(ls);
            ls.visible = false;
            ls.alpha = 0;
            k++;
         }
         _goalPP.visible = false;
      }

      private function goalIn() : void
      {
         var i:int = sideIndex(_goalSide);
         if(i < 0)
         {
            return;
         }
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
         tween(_goalBlueMask,"scaleX",1,0.24);
         later(0.24,"gCover",[g]);
         var W:Number = BAR_END - TEAM_X;
         var k:int = 0;
         while(k < 2)
         {
            var st:Shape = _streaks[k];
            tween(st,"x",W + 10,0.32,0.2 + k * 0.1);
            k++;
         }
         tween(_goalTeamMask,"scaleX",1,0.28,0.44);
         tween(_emboss,"x",_emboss.x + 26,1.1,0.44);
         tween(_goalLogoBox,"scaleX",1,0.4,0.44);
         tween(_goalLogoBox,"scaleY",1,0.4,0.44);
         later(0.95,"gGrow",[g]);
         later(1.25,"gZoom",[g]);
         later(1.36,"gName",[g]);
         later(1.88,"gBreak",[g]);
         later(2.12,"gLetters",[g]);
      }

      private function goalCover() : void
      {
         _covered = true;
         var i:int = 0;
         while(i < 2)
         {
            stopTweens(_rows[i]);
            Sprite(_rows[i]).alpha = 0;
            stopTweens(_score[i]);
            TextField(_score[i]).alpha = 0;
            i++;
         }
         stopTweens(_clock);
         _clock.alpha = 0;
         stopTweens(_clockText);
         stopTweens(_periodText);
         _clockText.alpha = 0;
         _periodText.alpha = 0;
         stopTweens(_shotsRow);
         _shotsRow.alpha = 0;
         _tile.alpha = 0;
      }

      private function goalSnapHeld() : void
      {
         goalCover();
         _goalBlueMask.scaleX = 1;
         _goalBlue.visible = false;
         _goalTeamMask.scaleX = 1;
         _goalLogoBox.visible = false;
         _emboss.alpha = 0;
         _nameBox.visible = false;
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

      private function goalName() : void
      {
         _goalBlue.visible = false;
         _nameBox.visible = true;
         _nameBox.alpha = 0;
         tween(_nameBox,"alpha",1,0.12);
         tween(_emboss,"alpha",0,0.15);
      }

      private function goalBreak() : void
      {
         var k:int = 0;
         while(k < 3)
         {
            var nt:TextField = _nameSlices[k];
            tween(nt,"x",nt.x + (k == 1 ? -70 : 70),0.14);
            tween(nt,"alpha",0,0.14);
            k++;
         }
         tween(_underline,"alpha",0,0.1);
      }

      private function goalLetters() : void
      {
         var order:Array = [0,3,2,1];
         var k:int = 0;
         while(k < _goalLetters.length)
         {
            var idx:int = k < order.length && int(order[k]) < _goalLetters.length ? int(order[k]) : k;
            var ls:Sprite = _goalLetters[idx];
            ls.visible = true;
            ls.alpha = 0;
            tween(ls,"alpha",1,0.12,k * 0.07);
            k++;
         }
         if(_goalIsPP)
         {
            _goalPP.visible = true;
            _goalPP.alpha = 0;
            tween(_goalPP,"alpha",1,0.2,0.2);
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
         var n:int = _goalLetters.length;
         var k:int = 0;
         while(k < n)
         {
            var ls:Sprite = _goalLetters[k];
            stopTweens(ls);
            var d:Number = k == 1 ? 0 : (k == 0 ? 0.12 : 0.06);
            tween(ls,"alpha",0,0.12,d);
            k++;
         }
         stopTweens(_goalPP);
         tween(_goalPP,"alpha",0,0.1);
         stopTweens(_goalTeamMask);
         _goalBlue.visible = false;
         tween(_goalTeamMask,"scaleX",0.02,0.3,0.06);
         later(0.38,"gRebuild",[_goalGen]);
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
            stopTweens(_rows[i]);
            Sprite(_rows[i]).alpha = 1;
            stopTweens(_score[i]);
            TextField(_score[i]).alpha = 1;
            i++;
         }
         stopTweens(_clock);
         _clock.alpha = 1;
         stopTweens(_clockText);
         stopTweens(_periodText);
         _clockText.alpha = 1;
         _periodText.alpha = 1;
         stopTweens(_shotsRow);
         _shotsRow.alpha = 1;
         _tile.alpha = 1;
         layoutRows();
      }

      private function goalRebuild() : void
      {
         _goal.visible = false;
         goalReset();
         _covered = false;
         var i:int = 0;
         while(i < 2)
         {
            tween(_rows[i],"alpha",1,0.14,i * 0.08);
            tween(_score[i],"alpha",1,0.2,0.3 + i * 0.04);
            i++;
         }
         tween(_clock,"alpha",1,0.14,0.16);
         tween(_tile,"alpha",1,0.14,0.16);
         tween(_shotsRow,"alpha",1,0.14,0.2);
         tween(_clockText,"alpha",1,0.2,0.38);
         tween(_periodText,"alpha",1,0.2,0.38);
         later(0.5,"afterGoal",null);
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
         paintTile();
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
         if(_warnText == null)
         {
            return;
         }
         var parts:Array = splitClock(param2 == null ? "" : param2);
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
         setFieldText(_shotsLabel,label == "" ? L_SHOTS : label,C_NAVY);
         if(label == "" || label.indexOf("SHOT") >= 0)
         {
            _hasShots = true;
            _lastShots[0] = _shots[0];
            _lastShots[1] = _shots[1];
         }
         else
         {
            ++_shotsGen;
            autoLater(SHOTS_HOLD,"autoShots",[_shotsGen]);
         }
         if(!_isShootout)
         {
            layoutShots();
         }
      }

      private var _lastShots:Array = ["",""];

      private function backToShots() : void
      {
         _shots[0] = _lastShots[0];
         _shots[1] = _lastShots[1];
         setFieldText(_shotsLabel,L_SHOTS,C_NAVY);
         if(!_isShootout)
         {
            layoutShots();
         }
      }

      public function hideComparison() : void
      {
      }

      public function showShootout(param1:Boolean = false) : void
      {
         _isShootout = true;
         setPeriod("SO");
         layoutShots();
         drawShootout();
         refreshBar();
      }

      public function hideShootout() : void
      {
         _isShootout = false;
         layoutShots();
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

      private function refreshBar(immediate:Boolean = false) : void
      {
         var tabText:String = null;
         var tabTime:String = null;
         var tabTeam:int = -2;
         var extraLabel:String = null;
         var extraTime:String = null;
         var tile:Boolean = false;
         if(_goalSide == AWAY || _goalSide == HOME || _covered)
         {
            tabText = null;
         }
         else
         {
            if(_warnText != null)
            {
               var wi:int = sideIndex(_warnSide);
               if(wi >= 0)
               {
                  tabTeam = wi;
                  tabText = isPowerPlay() && _warnText == "" ? L_PP : _warnText.toUpperCase();
                  if(tabText == "")
                  {
                     tabText = L_PP;
                  }
                  tabTime = _warnTime;
                  tile = PP_LOGO && isPowerPlay();
               }
               else
               {
                  extraLabel = _warnText.toUpperCase();
                  extraTime = _warnTime;
               }
            }
            if(extraLabel == null && _popupText != null && _popupText != "")
            {
               var pp:Array = splitClock(_popupText);
               extraLabel = String(pp[0]).toUpperCase();
               extraTime = pp[1];
            }
            if(tabText == null && _isShootout)
            {
               tabTeam = -1;
               tabText = L_SHOOTOUT + " - " + L_ROUND + " " + shootoutRound();
            }
         }
         setTile(tile,immediate);
         if(tabText == null)
         {
            closeTab(immediate);
         }
         else
         {
            openTab(tabTeam,tabText,tabTime,tile && tabTeam == 1,immediate);
         }
         if(extraLabel == null)
         {
            closeExtra(immediate);
         }
         else
         {
            openExtra(extraLabel,extraTime,immediate);
         }
         placeRight(immediate);
      }

      private function setTile(on:Boolean, immediate:Boolean) : void
      {
         paintTile();
         if(on == _tileOpen)
         {
            return;
         }
         _tileOpen = on;
         _clockShift = on ? TILE_W + GAP : 0;
         stopTweens(_tileMask);
         _tile.visible = true;
         if(immediate || _instant)
         {
            _tileMask.scaleX = on ? 1 : 0;
            _tile.visible = on;
         }
         else
         {
            tween(_tileMask,"scaleX",on ? 1 : 0,0.25,0,on ? null : "hideTile");
         }
      }

      private function placeRight(immediate:Boolean) : void
      {
         var cx:Number = CLOCK_X + _clockShift;
         var ex:Number = cx + CLOCK_W + GAP;
         if(immediate || _instant)
         {
            stopTweens(_clock);
            _clock.x = cx;
            _extra.x = ex;
         }
         else
         {
            if(Math.abs(_clock.x - cx) > 0.5)
            {
               tween(_clock,"x",cx,0.25);
            }
            if(Math.abs(_extra.x - ex) > 0.5)
            {
               tween(_extra,"x",ex,0.25);
            }
         }
         if(_sideOpen)
         {
            var sx:Number = sideX();
            if(immediate || _instant)
            {
               _side.x = _sideMask.x = sx;
            }
            else
            {
               tween(_side,"x",sx,0.25);
               tween(_sideMask,"x",sx,0.25);
            }
         }
      }

      private function sideX() : Number
      {
         return CLOCK_X + _clockShift + CLOCK_W + GAP + (_extraOpen ? EXTRA_W + GAP : 0);
      }

      private function openTab(team:int, label:String, time:String, wide:Boolean, immediate:Boolean) : void
      {
         var x:Number = team >= 0 ? TEAM_X + team * TEAM_W : TEAM_X;
         var w:Number = team >= 0 ? TEAM_W + (wide ? TILE_W + GAP : 0) : TEAM_W * 2;
         setFieldText(_tabLabel,label,C_TEXT);
         var hasTime:Boolean = time != null && time != "";
         setFieldText(_tabTime,hasTime ? time : "",C_TEXT);
         _tabTime.visible = hasTime;
         _tabLabel.scaleX = _tabLabel.scaleY = _tabTime.scaleX = _tabTime.scaleY = 1;
         w = Math.max(w,Math.round(_tabLabel.width + (hasTime ? _tabTime.width + 14 : 0) + 22));
         if(team == 0)
         {
            w = Math.min(w,TEAM_W * 2);
         }
         var c:uint = team >= 0 ? teamCol(team) : C_NAVY;
         var g:Graphics = _tabBg.graphics;
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(w,TAB_H,Math.PI / 2,0,0);
         g.beginGradientFill(GradientType.LINEAR,[mix(c,16777215,0.08),c,mix(c,0,0.16)],[1,1,1],[0,110,255],m);
         roundRect(g,0,0,w,TAB_H,0,RADIUS * 0.6);
         g.endFill();
         fit(_tabTime,w * 0.4);
         fit(_tabLabel,w - 20 - (hasTime ? _tabTime.width + 12 : 0));
         if(hasTime)
         {
            _tabLabel.x = 10;
            _tabTime.x = Math.round(w - 10 - _tabTime.width);
         }
         else
         {
            _tabLabel.x = Math.round((w - _tabLabel.width) / 2);
         }
         centerY(_tabLabel,0,TAB_H);
         centerY(_tabTime,0,TAB_H);
         var moved:Boolean = _tab.x != x;
         _tab.x = x;
         _tab.visible = true;
         stopTweens(_tab);
         if(immediate || _instant)
         {
            _tab.y = H;
         }
         else
         {
            if(moved && _tabOpen)
            {
               _tab.y = H - TAB_H;
            }
            tween(_tab,"y",H,0.25);
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
            _tab.y = H - TAB_H;
            _tab.visible = false;
         }
         else
         {
            tween(_tab,"y",H - TAB_H,0.2,0,"hideTab");
         }
      }

      private function openExtra(label:String, time:String, immediate:Boolean) : void
      {
         setFieldText(_extraLabel,label,C_NAVY);
         var hasTime:Boolean = time != null && time != "";
         setFieldText(_extraTime,hasTime ? time : "",C_NAVY);
         _extraTime.visible = hasTime;
         fit(_extraLabel,EXTRA_W - 10);
         fit(_extraTime,EXTRA_W - 10);
         _extraLabel.x = Math.round((EXTRA_W - _extraLabel.width) / 2);
         _extraTime.x = Math.round((EXTRA_W - _extraTime.width) / 2);
         if(hasTime)
         {
            centerY(_extraLabel,4,ROW_H * 0.5);
            centerY(_extraTime,ROW_H * 0.45,H - ROW_H * 0.45 - 4);
         }
         else
         {
            centerY(_extraLabel,0,H);
         }
         _extra.visible = true;
         stopTweens(_extra);
         if(immediate || _instant)
         {
            _extra.alpha = 1;
         }
         else if(!_extraOpen)
         {
            tween(_extra,"alpha",1,0.25);
         }
         _extraOpen = true;
      }

      private function closeExtra(immediate:Boolean) : void
      {
         if(!_extraOpen && !immediate)
         {
            return;
         }
         _extraOpen = false;
         stopTweens(_extra);
         if(immediate || _instant)
         {
            _extra.alpha = 0;
            _extra.visible = false;
         }
         else
         {
            tween(_extra,"alpha",0,0.2,0,"hideExtra");
         }
      }

      private function openSide(side:Number, s:String, s2:String) : void
      {
         var i:int = sideIndex(side);
         var c:uint = i >= 0 ? teamCol(i) : C_SIDE;
         setFieldText(_sideText,s.toUpperCase(),C_TEXT);
         var two:Boolean = s2 != null && s2 != "";
         setFieldText(_sideText2,two ? s2.toUpperCase() : "",C_TEXT);
         _sideText2.visible = two;
         fit(_sideText,520);
         fit(_sideText2,520);
         var w:Number = Math.round(Math.max(_sideText.width,two ? _sideText2.width : 0) + 32);
         var g:Graphics = _sideBg.graphics;
         g.clear();
         var m:Matrix = new Matrix();
         m.createGradientBox(w,H,Math.PI / 2,0,0);
         g.beginGradientFill(GradientType.LINEAR,[mix(c,16777215,0.08),c,mix(c,0,0.18)],[1,1,1],[0,110,255],m);
         roundRect(g,0,0,w,H,RADIUS * 0.5,RADIUS);
         g.endFill();
         _sideText.x = 16;
         _sideText2.x = 16;
         if(two)
         {
            centerY(_sideText,2,H * 0.55);
            centerY(_sideText2,H * 0.5,H * 0.5 - 4);
         }
         else
         {
            centerY(_sideText,0,H);
         }
         var x:Number = sideX();
         var mg:Graphics = _sideMask.graphics;
         mg.clear();
         mg.beginFill(0);
         mg.drawRect(0,0,w,H);
         mg.endFill();
         stopTweens(_side);
         stopTweens(_sideMask);
         _side.x = x;
         _sideMask.x = x;
         _side.visible = true;
         _sideMask.scaleX = 0;
         tween(_sideMask,"scaleX",1,0.3);
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
         stopTweens(_sideMask);
         if(immediate || _instant)
         {
            _side.visible = false;
            _sideMask.scaleX = 0;
         }
         else
         {
            tween(_sideMask,"scaleX",0,0.2,0,"hideSide");
         }
      }

      private function drawShootout() : void
      {
         var g:Graphics = _shootoutDots.graphics;
         g.clear();
         var n:int = _shootout != null && _shootout.numRounds != null ? int(_shootout.numRounds) : 3;
         var cur:int = shootoutRound();
         var show:int = Math.max(n,cur);
         var w:Number = TEAM_W * 2;
         var cell:Number = w / 2;
         var r:Number = Math.max(5,Math.min(9,SHOTS_H * 0.27));
         var step:Number = r * 2 + 8;
         var fitN:int = Math.max(1,int((cell - 8) / step));
         var first:int = show > fitN ? show - fitN + 1 : 1;
         var i:int = 0;
         while(i < 2)
         {
            var side:String = i == 0 ? "away" : "home";
            var cx0:Number = i == 0 ? 0 : w - cell;
            var cnt:int = show - first + 1;
            var k:int = first;
            while(k <= show)
            {
               var v:Number = _shootout != null ? Number(_shootout[side + "Round" + k]) : 0;
               var cx:Number = cx0 + cell / 2 + (k - first - (cnt - 1) / 2) * step;
               var cy:Number = SHOTS_H / 2;
               if(v == 1)
               {
                  g.lineStyle();
                  g.beginFill(C_NAVY);
                  g.drawCircle(cx,cy,r);
                  g.endFill();
                  g.lineStyle(2,16777215);
                  g.moveTo(cx - r * 0.45,cy);
                  g.lineTo(cx - r * 0.1,cy + r * 0.4);
                  g.lineTo(cx + r * 0.5,cy - r * 0.4);
               }
               else if(v == 2)
               {
                  g.lineStyle();
                  g.beginFill(C_MISS);
                  g.drawCircle(cx,cy,r);
                  g.endFill();
                  g.lineStyle(2,16777215);
                  g.moveTo(cx - r * 0.4,cy - r * 0.4);
                  g.lineTo(cx + r * 0.4,cy + r * 0.4);
                  g.moveTo(cx + r * 0.4,cy - r * 0.4);
                  g.lineTo(cx - r * 0.4,cy + r * 0.4);
               }
               else
               {
                  g.lineStyle(2,C_NAVY);
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
         t.y -= 12;
         later(0,"scoreIn",[i]);
      }

      private function scoreIn(i:int) : void
      {
         var t:TextField = _score[i];
         centerY(t,0,ROW_H);
         var y:Number = t.y;
         t.y = y + 12;
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
         else if(action == "gGrow" && int(args[0]) == _goalGen && _goalUp)
         {
            tween(_goalLogoBox,"scaleX",1.3,0.3);
            tween(_goalLogoBox,"scaleY",1.3,0.3);
         }
         else if(action == "gZoom" && int(args[0]) == _goalGen && _goalUp)
         {
            stopTweens(_goalLogoBox);
            tween(_goalLogoBox,"scaleX",7,0.16);
            tween(_goalLogoBox,"scaleY",7,0.16);
            tween(_goalLogoBox,"alpha",0,0.16);
         }
         else if(action == "gName" && int(args[0]) == _goalGen && _goalUp)
         {
            goalName();
         }
         else if(action == "gBreak" && int(args[0]) == _goalGen && _goalUp)
         {
            goalBreak();
         }
         else if(action == "gLetters" && int(args[0]) == _goalGen && _goalUp)
         {
            _nameBox.visible = false;
            _goalLogoBox.visible = false;
            goalLetters();
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
         else if(action == "hideExtra" && !_extraOpen)
         {
            _extra.visible = false;
         }
         else if(action == "hideTile" && !_tileOpen)
         {
            _tile.visible = false;
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
