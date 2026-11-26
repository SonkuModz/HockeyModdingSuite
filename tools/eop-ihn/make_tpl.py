"""Writes EndOfPeriod.tpl.as: the end-of-period class, sharing the band drawing, logo art, logo loading and colour
helpers with ../intro-ihn/TeamIntro.tpl.as (copied by line range; re-run after changing those parts of the intro).

usage: python make_tpl.py
"""
import os, re

here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, '..', 'intro-ihn', 'TeamIntro.tpl.as'), encoding='utf-8').read()

def block(start, end):
    """The intro's source from the line starting `start` up to (not including) the line starting `end`."""
    a = src.index(start)
    b = src.index(end, a)
    return src[a:b]

shared = (block('      private function halfPath', '      private function paintTab')
          + block('      private function drawArt', '      // ------------------------------------------------------------------ animation')
          + block('      private function glint', '      private function apply')
          + block('      private function tf(', '      // ------------------------------------------------------------------ preview'))

head = open(os.path.join(here, 'head.as'), encoding='utf-8').read()
tail = open(os.path.join(here, 'tail.as'), encoding='utf-8').read()
out = head + shared + tail
open(os.path.join(here, 'EndOfPeriod.tpl.as'), 'w', encoding='utf-8', newline='\n').write(out)
print(len(out), 'chars')
