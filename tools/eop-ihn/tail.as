      // ------------------------------------------------------------------ preview (Ruffle, flashvars)

      private var _q:Object;

      private function v(k:String, d:String) : String
      {
         return _q[k] != null ? String(_q[k]) : d;
      }

      private function previewRun() : void
      {
         _q = loaderInfo.parameters;
         SetData({
            "AwayTeamCity":v("awayCity", "Tampa Bay"),
            "AwayTeamName":v("awayName", "Lightning"),
            "HomeTeamCity":v("homeCity", "Toronto"),
            "HomeTeamName":v("homeName", "Maple Leafs"),
            "AwayPrimaryTeamColor":uint(v("awayColor", "0x002868")),
            "AwaySecondaryTeamColor":uint(v("awaySecondary", "0xFFFFFF")),
            "HomePrimaryTeamColor":uint(v("homeColor", "0x00205B")),
            "HomeSecondaryTeamColor":uint(v("homeSecondary", "0xFFFFFF")),
            "AwayTeamScore":int(v("awayScore", "2")),
            "HomeTeamScore":int(v("homeScore", "1")),
            "PeriodString":v("period", "End of 1st"),
            "AwayTeamLogoPath":v("awayLogo", "TBL.png"),
            "HomeTeamLogoPath":v("homeLogo", "TOR.png")
         });
         show();
      }
   }
}
