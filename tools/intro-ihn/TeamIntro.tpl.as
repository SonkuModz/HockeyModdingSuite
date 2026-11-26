package overlays.teamintro
{
   import flash.display.BlendMode;
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
   import flash.geom.Rectangle;
   import flash.net.URLRequest;
   import flash.text.TextField;
   import flash.text.TextFieldAutoSize;
   import flash.text.TextFormat;
   import flash.utils.getDefinitionByName;
   import flash.utils.getTimer;

   // IHN (Ice Hockey Network) pre-game team intro for NHL 24, design "B Broadcast Band": one long gunmetal band across
   // the bottom (away left, home right), a centre plate with the IHN logo and a gold VS, record tabs under the band,
   // "TONIGHT ON IHN" and the arena above it. Drawn in code; the game drives it like EA's movie (TeamVsTeam sends
   // init, show, hide, setOnHideCompleteCallback, dispose).
   public class TeamIntro extends MovieClip
   {
      private static const ART:Array = __ART__;

      private static const G1:uint = 0x2A2E35;
      private static const G2:uint = 0x0E1014;
      private static const P1:uint = 0x23272E;
      private static const P2:uint = 0x0B0D10;
      private static const RIM:uint = 0x8B919B;
      private static const GOLD:uint = 0xF2A83A;
      private static const CITY:uint = 0xD7DEE7;

      private static const BX:Number = 90;
      private static const BY:Number = 770;
      private static const HW:Number = 800;
      private static const HH:Number = 190;
      private static const SL:Number = 60;
      private static const LOGO:Number = 160;
      private static const TEXT_W:Number = 340;
      private static const SHOW_T:Number = 1.1;

      private var _preview:Boolean = false;
      private var _fontHead:String = "$Header";
      private var _fontHeadItalic:String = "$HeaderItalic";
      private var _fontBody:String = "$Default";
      private var _fontAlt:String = "$BodyAlternate";

      private var _all:Sprite;
      private var _half:Array = [];
      private var _halfArt:Array = [];
      private var _glint:Array = [];
      private var _logo:Array = [];
      private var _logoPath:Array = ["", ""];
      private var _city:Array = [];
      private var _name:Array = [];
      private var _tab:Array = [];
      private var _tabArt:Array = [];
      private var _tabLabel:Array = [];
      private var _tabValue:Array = [];
      private var _mid:Sprite;
      private var _vs:TextField;
      private var _tonight:TextField;
      private var _arena:TextField;
      private var _hasTab:Array = [false, false];

      private var _cb:Function = null;
      private var _p:Number = 0;
      private var _dir:int = 0;
      private var _last:int = 0;
      private var _shown:Boolean = false;
      private var _disposed:Boolean = false;

      public function TeamIntro()
      {
         super();
         stop();
         try
         {
            _preview = loaderInfo != null && loaderInfo.parameters != null && loaderInfo.parameters.nhl24kitPreview != null;
         }
         catch(e:Error)
         {
            _preview = false;
         }
         if(_preview)
         {
            _fontHead = _fontHeadItalic = _fontBody = _fontAlt = "_sans";
         }
         build();
         apply(0);
         addEventListener(Event.ENTER_FRAME, tick);
         if(_preview)
         {
            previewRun();
         }
      }

      // ------------------------------------------------------------------ the game's API

      public function init(d:Object) : void
      {
         if(d == null || _disposed)
         {
            return;
         }
         var c:Array = [colourOf(d.awayTeamColor, d.awaySecondaryTeamColor), colourOf(d.homeTeamColor, d.homeSecondaryTeamColor)];
         var city:Array = [str(d.awayCityName), str(d.homeCityName)];
         var nm:Array = [str(d.awayTeamName), str(d.homeTeamName)];
         var rec:Array = [str(d.awayTeamRecord), str(d.homeTeamRecord)];
         var sig:Array = [str(d.awaySignificance), str(d.homeSignificance)];
         var path:Array = [str(d.awayLogoPath), str(d.homeLogoPath)];
         for(var i:int = 0; i < 2; i++)
         {
            paintHalf(i, c[i]);
            setText(_city[i], city[i], _fontBody, 26, CITY, 7, i == 1);
            setText(_name[i], nm[i], _fontHead, 76, 0xFFFFFF, 2, i == 1);
            _name[i].filters = [new GlowFilter(light(c[i]), 0.9, 14, 14, 2, 2), new DropShadowFilter(3, 90, 0, 0.9, 5, 5, 1, 2)];
            fitText(_city[i], TEXT_W, 26, 14);
            fitText(_name[i], TEXT_W, 76, 34);
            placeText(i);
            var label:String = sig[i] != "" ? sig[i].toUpperCase() : (rec[i] != "" ? "RECORD" : "");
            _hasTab[i] = label != "";
            setText(_tabLabel[i], label, _fontBody, 24, 0xFFFFFF, 2, false);
            setText(_tabValue[i], sig[i] != "" ? "" : rec[i], _fontBody, 28, GOLD, 2, false);
            paintTab(i, c[i]);
            if(path[i] != _logoPath[i])
            {
               _logoPath[i] = path[i];
               loadLogo(_logo[i], path[i]);
            }
         }
         var an:String = str(d.arenaName);
         var al:String = str(d.arenaLocation);
         var html:String = "";
         if(an != "")
         {
            html = "<font color=\"#F2A83A\">" + esc(an) + "</font>";
         }
         if(al != "")
         {
            html += (html != "" ? "  ·  " : "") + esc(al);
         }
         _arena.htmlText = html;
         centre(_arena, 684);
         _vs.text = "VS";
         centre(_vs, 0);
         _vs.x = 230 - _vs.width / 2;
         apply(_p);
      }

      public function show() : void
      {
         if(_disposed)
         {
            return;
         }
         _shown = true;
         _dir = 1;
         _last = getTimer();
      }

      public function hide() : void
      {
         if(_disposed)
         {
            return;
         }
         if(_p <= 0)
         {
            _dir = 0;
            done();
            return;
         }
         _dir = -1;
         _last = getTimer();
      }

      public function setOnHideCompleteCallback(f:Function) : void
      {
         _cb = f;
      }

      public function SetData(d:Object, f:Function = null) : void
      {
         init(d);
      }

      public function dispose() : void
      {
         _disposed = true;
         _dir = 0;
         removeEventListener(Event.ENTER_FRAME, tick);
         for(var i:int = 0; i < 2; i++)
         {
            if(_logo[i] != null)
            {
               clearKids(_logo[i]);
            }
         }
         _cb = null;
      }

      private function done() : void
      {
         var f:Function = _cb;
         if(f != null)
         {
            try
            {
               f();
            }
            catch(e:Error)
            {
            }
         }
      }

      // ------------------------------------------------------------------ building

      private function build() : void
      {
         _all = new Sprite();
         addChild(_all);
         _tonight = tf(_fontHeadItalic);
         _all.addChild(_tonight);
         setText(_tonight, "TONIGHT ON IHN", _fontHeadItalic, 26, 0xDFE6EE, 12, false);
         _tonight.filters = [new DropShadowFilter(2, 90, 0, 0.9, 6, 6, 1, 2)];
         centre(_tonight, 636);
         _arena = tf(_fontAlt);
         _all.addChild(_arena);
         setText(_arena, "", _fontAlt, 30, 0xFFFFFF, 6, false);
         _arena.filters = [new DropShadowFilter(2, 90, 0, 0.9, 6, 6, 1, 2)];
         for(var i:int = 0; i < 2; i++)
         {
            // record tab (behind the band, drops out of it)
            var tab:Sprite = new Sprite();
            tab.x = i == 0 ? BX + 240 : BX + HW * 2 + 140 - 240 - 300;
            tab.y = BY + 196;
            var ta:Shape = new Shape();
            tab.addChild(ta);
            _tabArt[i] = ta;
            _tabLabel[i] = tf(_fontBody);
            _tabValue[i] = tf(_fontBody);
            tab.addChild(_tabLabel[i]);
            tab.addChild(_tabValue[i]);
            _all.addChild(tab);
            _tab[i] = tab;
         }
         var band:Sprite = new Sprite();
         band.filters = [new DropShadowFilter(10, 90, 0, 0.65, 18, 18, 1, 2)];
         _all.addChild(band);
         for(i = 0; i < 2; i++)
         {
            var h:Sprite = new Sprite();
            h.x = i == 0 ? BX : BX + HW + 140;
            h.y = BY;
            var art:Sprite = new Sprite();
            h.addChild(art);
            _halfArt[i] = art;
            var g:Sprite = new Sprite();
            var gm:Shape = new Shape();
            halfPath(gm.graphics, i, 0);
            g.addChild(gm);
            g.mask = gm;
            var gl:Shape = new Shape();
            g.addChild(gl);
            h.addChild(g);
            _glint[i] = gl;
            var lh:Sprite = new Sprite();
            lh.x = i == 0 ? 90 : HW - 90 - LOGO;
            lh.y = 15;
            h.addChild(lh);
            _logo[i] = lh;
            _city[i] = tf(_fontBody);
            _name[i] = tf(_fontHead);
            h.addChild(_city[i]);
            h.addChild(_name[i]);
            band.addChild(h);
            _half[i] = h;
            paintHalf(i, i == 0 ? 0x2C6BD6 : 0xC8102E);
         }
         _mid = new Sprite();
         var mp:Sprite = new Sprite();
         mp.x = -230;
         mp.y = -129;
         _mid.x = 960;
         _mid.y = BY - 34 + 129;
         var m:Graphics = mp.graphics;
         var mx:Matrix = new Matrix();
         mx.createGradientBox(460, 258, Math.PI / 2, 0, 0);
         m.lineStyle(3, RIM, 1, true);
         m.beginGradientFill(GradientType.LINEAR, [P1, P2], [1, 1], [0, 255], mx);
         m.moveTo(1.5, 1.5);
         m.lineTo(458.5, 1.5);
         m.lineTo(419, 256.5);
         m.lineTo(41, 256.5);
         m.lineTo(1.5, 1.5);
         m.endFill();
         m.lineStyle();
         mx = new Matrix();
         mx.createGradientBox(360, 4, 0, 50, 8);
         m.beginGradientFill(GradientType.LINEAR, [GOLD, GOLD, GOLD], [0, 1, 0], [0, 128, 255], mx);
         m.drawRect(50, 8, 360, 4);
         m.endFill();
         var logo:Sprite = new Sprite();
         drawArt(logo.graphics);
         logo.scaleX = logo.scaleY = 0.58;
         logo.x = 230 - 300 * 0.58;
         logo.y = 8;
         logo.filters = [new DropShadowFilter(4, 90, 0, 0.6, 8, 8, 1, 2)];
         mp.addChild(logo);
         _vs = tf(_fontHeadItalic);
         setText(_vs, "VS", _fontHeadItalic, 64, GOLD, 6, false);
         _vs.filters = [new GlowFilter(GOLD, 0.45, 16, 16, 2, 2)];
         _vs.x = 230 - _vs.width / 2;
         _vs.y = 150;
         mp.addChild(_vs);
         _mid.addChild(mp);
         band.addChild(_mid);
      }

      private function halfPath(g:Graphics, i:int, inset:Number) : void
      {
         var a:Number = inset;
         if(i == 0)
         {
            g.moveTo(SL + a * 0.6, a);
            g.lineTo(HW - a, a);
            g.lineTo(HW - a, HH - a);
            g.lineTo(a * 1.6, HH - a);
            g.lineTo(SL + a * 0.6, a);
         }
         else
         {
            g.moveTo(a, a);
            g.lineTo(HW - SL - a * 0.6, a);
            g.lineTo(HW - a * 1.6, HH - a);
            g.lineTo(a, HH - a);
            g.lineTo(a, a);
         }
      }

      private function paintHalf(i:int, c:uint) : void
      {
         var c2:uint = light(c);
         var art:Sprite = _halfArt[i];
         clearKids(art);
         var base:Shape = new Shape();
         var g:Graphics = base.graphics;
         var mx:Matrix = new Matrix();
         mx.createGradientBox(HW, HH, Math.PI / 2, 0, 0);
         g.beginGradientFill(GradientType.LINEAR, [G1, G2], [1, 1], [0, 255], mx);
         halfPath(g, i, 0);
         g.endFill();
         art.addChild(base);
         // team glow: a soft spot behind the logo and diagonal streaks, screened over the gunmetal
         var glow:Sprite = new Sprite();
         glow.blendMode = BlendMode.SCREEN;
         glow.alpha = 0.8;
         var gs:Shape = new Shape();
         g = gs.graphics;
         var cx:Number = i == 0 ? HW * 0.24 : HW * 0.76;
         mx = new Matrix();
         mx.createGradientBox(720, 342, 0, cx - 360, HH * 0.55 - 171);
         g.beginGradientFill(GradientType.RADIAL, [c2, c2], [1, 0], [0, 178], mx);
         g.drawRect(0, 0, HW, HH);
         g.endFill();
         var k:Number = Math.tan(28 * Math.PI / 180) * HH;
         g.beginFill(c, 1);
         for(var x:Number = -k - 60; x < HW + k + 60; x += 56 / Math.cos(28 * Math.PI / 180))
         {
            if(i == 0)
            {
               g.moveTo(x, 0);
               g.lineTo(x + 3.4, 0);
               g.lineTo(x + 3.4 + k, HH);
               g.lineTo(x + k, HH);
            }
            else
            {
               g.moveTo(x + k, 0);
               g.lineTo(x + k + 3.4, 0);
               g.lineTo(x + 3.4, HH);
               g.lineTo(x, HH);
            }
         }
         g.endFill();
         glow.addChild(gs);
         var gm:Shape = new Shape();
         gm.graphics.beginFill(0);
         halfPath(gm.graphics, i, 0);
         gm.graphics.endFill();
         glow.addChild(gm);
         gs.mask = gm;
         art.addChild(glow);
         // rim and the bottom streak
         var r:Shape = new Shape();
         g = r.graphics;
         g.lineStyle(3, RIM, 1, true);
         halfPath(g, i, 1.5);
         g.lineStyle();
         mx = new Matrix();
         mx.createGradientBox(HW - 80, 4, 0, 40, HH - 16);
         g.beginGradientFill(GradientType.LINEAR, [c2, c2, 0xFFFFFF, c2, c2], [0, 1, 1, 1, 0], [0, 70, 128, 185, 255], mx);
         g.drawRect(40, HH - 16, HW - 80, 4);
         g.endFill();
         art.addChild(r);
         // the glint that sweeps across once the band is in
         g = _glint[i].graphics;
         g.clear();
         mx = new Matrix();
         mx.createGradientBox(160, HH, 0, -80, 0);
         g.beginGradientFill(GradientType.LINEAR, [0xFFFFFF, 0xFFFFFF, 0xFFFFFF], [0, 0.22, 0], [0, 128, 255], mx);
         g.moveTo(-80, 0);
         g.lineTo(80, 0);
         g.lineTo(80 - 50, HH);
         g.lineTo(-80 - 50, HH);
         g.endFill();
         _logo[i].filters = [new GlowFilter(c2, 0.8, 18, 18, 2, 2), new DropShadowFilter(3, 90, 0, 0.9, 4, 4, 1, 2)];
      }

      private function paintTab(i:int, c:uint) : void
      {
         var g:Graphics = _tabArt[i].graphics;
         g.clear();
         var mx:Matrix = new Matrix();
         mx.createGradientBox(300, 46, Math.PI / 2, 0, 0);
         g.lineStyle(2, light(c), 1, true);
         g.beginGradientFill(GradientType.LINEAR, [P1, P2], [1, 1], [0, 255], mx);
         g.moveTo(1, 1);
         g.lineTo(299, 1);
         g.lineTo(283, 45);
         g.lineTo(17, 45);
         g.lineTo(1, 1);
         g.endFill();
         _tabArt[i].filters = [new GlowFilter(c, 0.7, 14, 14, 1.5, 2, true)];
         var l:TextField = _tabLabel[i];
         var v:TextField = _tabValue[i];
         var gap:Number = v.text != "" ? 12 : 0;
         var w:Number = l.width + gap + v.width;
         l.x = 150 - w / 2;
         v.x = l.x + l.width + gap;
         l.y = 23 - l.height / 2;
         v.y = 23 - v.height / 2;
         _tab[i].visible = _hasTab[i];
      }

      private function placeText(i:int) : void
      {
         var c:TextField = _city[i];
         var n:TextField = _name[i];
         if(i == 0)
         {
            c.x = 280;
            n.x = 280;
         }
         else
         {
            c.x = HW - 280 - c.width;
            n.x = HW - 280 - n.width;
         }
         c.y = 52 - c.height / 2;
         n.y = 118 - n.height / 2;
      }

      private function drawArt(g:Graphics) : void
      {
         for each(var it:Array in ART)
         {
            var b:Rectangle = null;
            for each(var con:Array in it[4])
            {
               for(var j:int = 0; j < con.length; j += 2)
               {
                  if(b == null)
                  {
                     b = new Rectangle(con[j], con[j + 1], 0, 0);
                  }
                  else
                  {
                     b = b.union(new Rectangle(con[j], con[j + 1], 0.01, 0.01));
                  }
               }
            }
            if(it[2] >= 0)
            {
               g.lineStyle(it[3], it[2], it[1]);
            }
            else
            {
               g.lineStyle();
            }
            if(it[0] is String)
            {
               var mx:Matrix = new Matrix();
               var n:String = it[0];
               if(n == "silver")
               {
                  mx.createGradientBox(b.width, b.height, Math.PI / 2, b.x, b.y);
                  g.beginGradientFill(GradientType.LINEAR, [0xFFFFFF, 0xE2E8F0, 0x94A3B8, 0xF8FAFC], [1, 1, 1, 1], [0, 115, 128, 255], mx);
               }
               else if(n == "bg")
               {
                  mx.createGradientBox(b.width, b.height, Math.atan2(b.height, b.width), b.x, b.y);
                  g.beginGradientFill(GradientType.LINEAR, [0x071224, 0x0E2445], [1, 1], [0, 255], mx);
               }
               else if(n == "ice")
               {
                  mx.createGradientBox(b.width, b.height, 0, b.x, b.y);
                  g.beginGradientFill(GradientType.LINEAR, [0x00D2FF, 0x0066CC], [1, 1], [0, 255], mx);
               }
               else
               {
                  mx.createGradientBox(b.width, b.height, Math.atan2(b.height, b.width), b.x, b.y);
                  g.beginGradientFill(GradientType.LINEAR, [0xFDE047, 0xEAB308, 0xCA8A04], [1, 1, 1], [0, 128, 255], mx);
               }
            }
            else
            {
               g.beginFill(it[0], it[1]);
            }
            for each(con in it[4])
            {
               g.moveTo(con[0], con[1]);
               for(j = 2; j < con.length; j += 2)
               {
                  g.lineTo(con[j], con[j + 1]);
               }
               g.lineTo(con[0], con[1]);
            }
            g.endFill();
         }
         g.lineStyle();
      }

      // ------------------------------------------------------------------ logos

      private function loadLogo(holder:Sprite, path:String) : void
      {
         clearKids(holder);
         if(path == null || path == "")
         {
            return;
         }
         // the game's loader resets its own clip's scale, so the fit goes on a wrapper it never touches
         var wrap:Sprite = new Sprite();
         wrap.scaleX = wrap.scaleY = LOGO / 256;
         holder.addChild(wrap);
         var clip:MovieClip = new MovieClip();
         wrap.addChild(clip);
         for each(var cn:String in ["loader.DeprecatedImageLoader", "loader.ImageLoader"])
         {
            var lc:Class = null;
            try
            {
               lc = getDefinitionByName(cn) as Class;
            }
            catch(e:Error)
            {
               lc = null;
            }
            if(lc != null)
            {
               try
               {
                  new lc(clip, path, {"x":0, "y":0});
                  return;
               }
               catch(e2:Error)
               {
               }
            }
         }
         var l:Loader = new Loader();
         clip.addChild(l);
         try
         {
            l.load(new URLRequest(path));
         }
         catch(e3:Error)
         {
         }
      }

      private function fitLogos() : void
      {
         for(var i:int = 0; i < 2; i++)
         {
            var h:Sprite = _logo[i];
            if(h == null || h.numChildren == 0)
            {
               continue;
            }
            var wrap:Sprite = h.getChildAt(0) as Sprite;
            if(wrap == null || wrap.numChildren == 0)
            {
               continue;
            }
            var r:Rectangle = wrap.getChildAt(0).getBounds(wrap);
            if(r.width < 2 || r.height < 2)
            {
               continue;
            }
            var s:Number = LOGO / Math.max(r.width, r.height);
            if(Math.abs(wrap.scaleX - s) > 0.0001)
            {
               wrap.scaleX = wrap.scaleY = s;
            }
            wrap.x = (LOGO - r.width * s) / 2 - r.x * s;
            wrap.y = (LOGO - r.height * s) / 2 - r.y * s;
         }
      }

      // ------------------------------------------------------------------ animation

      private function tick(e:Event) : void
      {
         fitLogos();
         var now:int = getTimer();
         var dt:Number = Math.min(0.1, (now - _last) / 1000);
         _last = now;
         if(_dir == 0)
         {
            if(_p >= 1)
            {
               glint(now);
            }
            return;
         }
         if(_dir > 0)
         {
            _p = Math.min(1, _p + dt / SHOW_T);
            if(_p >= 1)
            {
               _dir = 0;
               _glintStart = now;
            }
         }
         else
         {
            _p = Math.max(0, _p - dt / 0.45);
            if(_p <= 0)
            {
               _dir = 0;
               apply(0);
               done();
               return;
            }
         }
         apply(_p);
      }

      private var _glintStart:int = 0;

      private function glint(now:int) : void
      {
         // one sweep across each half, then again every 6 seconds
         var t:Number = ((now - _glintStart) / 1000) % 6;
         var q:Number = Math.min(1, t / 0.9);
         for(var i:int = 0; i < 2; i++)
         {
            var g:Shape = _glint[i];
            g.visible = q < 1;
            g.x = i == 0 ? -140 + q * (HW + 280) : HW + 140 - q * (HW + 280);
         }
      }

      private static function seg(p:Number, a:Number, b:Number) : Number
      {
         var t:Number = (p * SHOW_T - a) / (b - a);
         t = t < 0 ? 0 : (t > 1 ? 1 : t);
         return 1 - Math.pow(1 - t, 3);
      }

      private function apply(p:Number) : void
      {
         _all.visible = p > 0;
         var a:Number = seg(p, 0, 0.5);
         _half[0].x = BX - (1 - a) * 1000;
         _half[1].x = BX + HW + 140 + (1 - a) * 1000;
         _half[0].alpha = _half[1].alpha = Math.min(1, a * 1.6);
         var m:Number = seg(p, 0.3, 0.7);
         _mid.scaleX = _mid.scaleY = 0.6 + 0.4 * m;
         _mid.alpha = m;
         var t:Number = seg(p, 0.55, 0.85);
         for(var i:int = 0; i < 2; i++)
         {
            _tab[i].y = BY + 196 - (1 - t) * 46;
            _tab[i].alpha = t;
            _glint[i].visible = false;
         }
         var u:Number = seg(p, 0.7, 1.1);
         _tonight.alpha = _arena.alpha = u;
         _tonight.y = 636 + (1 - u) * 20;
         _arena.y = 684 + (1 - u) * 20;
      }

      // ------------------------------------------------------------------ text and colour helpers

      private function tf(font:String) : TextField
      {
         var t:TextField = new TextField();
         t.selectable = false;
         t.mouseEnabled = false;
         t.embedFonts = false;
         t.autoSize = TextFieldAutoSize.LEFT;
         t.multiline = false;
         t.wordWrap = false;
         return t;
      }

      private function setText(t:TextField, s:String, font:String, size:Number, colour:uint, spacing:Number, right:Boolean) : void
      {
         var f:TextFormat = new TextFormat(font, size, colour);
         f.letterSpacing = spacing;
         t.defaultTextFormat = f;
         t.text = s;
         t.setTextFormat(f);
      }

      private function fitText(t:TextField, maxW:Number, size:Number, minSize:Number) : void
      {
         var f:TextFormat = t.getTextFormat();
         var s:Number = size;
         while(t.width > maxW && s > minSize)
         {
            s -= 2;
            f.size = s;
            t.setTextFormat(f);
            t.defaultTextFormat = f;
         }
      }

      private function centre(t:TextField, y:Number) : void
      {
         t.x = 960 - t.width / 2;
         if(y > 0)
         {
            t.y = y;
         }
      }

      private static function clearKids(c:Sprite) : void
      {
         while(c.numChildren > 0)
         {
            c.removeChildAt(0);
         }
      }

      private static function str(o:*) : String
      {
         return o == null ? "" : String(o);
      }

      private static function esc(s:String) : String
      {
         return s.split("&").join("&amp;").split("<").join("&lt;").split(">").join("&gt;");
      }

      private static function luma(c:uint) : Number
      {
         return (0.299 * (c >> 16 & 255) + 0.587 * (c >> 8 & 255) + 0.114 * (c & 255)) / 255;
      }

      // the team colour, brightened (same hue) so dark navies and reds still glow; the secondary when the primary is
      // black or grey
      private static function colourOf(p:*, s:*) : uint
      {
         var c:uint = p == null ? 0x8B919B : uint(p);
         if(flat(c))
         {
            var c2:uint = s == null ? 0x8B919B : uint(s);
            c = flat(c2) ? 0x8B919B : c2;
         }
         var r:Number = c >> 16 & 255;
         var g:Number = c >> 8 & 255;
         var b:Number = c & 255;
         var m:Number = Math.max(r, Math.max(g, b));
         if(m < 190)
         {
            var k:Number = 190 / m;
            r *= k;
            g *= k;
            b *= k;
         }
         return uint(Math.min(255, r)) << 16 | uint(Math.min(255, g)) << 8 | uint(Math.min(255, b));
      }

      private static function flat(c:uint) : Boolean
      {
         var r:Number = c >> 16 & 255;
         var g:Number = c >> 8 & 255;
         var b:Number = c & 255;
         var m:Number = Math.max(r, Math.max(g, b));
         return m < 24 || m - Math.min(r, Math.min(g, b)) < m * 0.2;
      }

      private static function light(c:uint) : uint
      {
         var r:Number = c >> 16 & 255;
         var g:Number = c >> 8 & 255;
         var b:Number = c & 255;
         r += (255 - r) * 0.35;
         g += (255 - g) * 0.35;
         b += (255 - b) * 0.35;
         return uint(r) << 16 | uint(g) << 8 | uint(b);
      }

      // ------------------------------------------------------------------ preview (Ruffle, flashvars)

      private var _q:Object;
      private var _hideAt:Number = 0;
      private var _start:int = 0;

      private function v(k:String, d:String) : String
      {
         return _q[k] != null ? String(_q[k]) : d;
      }

      private function previewRun() : void
      {
         _q = loaderInfo.parameters;
         init({
            "awayCityName":v("awayCity", "EDMONTON"),
            "awayTeamName":v("awayName", "OILERS"),
            "homeCityName":v("homeCity", "CALGARY"),
            "homeTeamName":v("homeName", "FLAMES"),
            "awayTeamColor":uint(v("awayColor", "0x041E42")),
            "awaySecondaryTeamColor":uint(v("awaySecondary", "0xFF4C00")),
            "homeTeamColor":uint(v("homeColor", "0xC8102E")),
            "homeSecondaryTeamColor":uint(v("homeSecondary", "0xF1BE48")),
            "awayTeamRecord":v("awayRecord", "12-5-2"),
            "homeTeamRecord":v("homeRecord", "9-8-3"),
            "awaySignificance":v("awaySig", ""),
            "homeSignificance":v("homeSig", ""),
            "arenaName":v("arena", "ROGERS PLACE"),
            "arenaLocation":v("location", "EDMONTON, AB"),
            "awayLogoPath":v("awayLogo", "EDM.png"),
            "homeLogoPath":v("homeLogo", "CGY.png")
         });
         show();
         _hideAt = Number(v("hideAt", "0"));
         _start = getTimer();
         if(_hideAt > 0)
         {
            addEventListener(Event.ENTER_FRAME, previewTick);
         }
      }

      private function previewTick(e:Event) : void
      {
         if(getTimer() - _start > _hideAt * 1000)
         {
            removeEventListener(Event.ENTER_FRAME, previewTick);
            hide();
         }
      }
   }
}
