from fontTools.pens.recordingPen import DecomposingRecordingPen, RecordingPen
from fontTools.pens.areaPen import AreaPen
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.reverseContourPen import ReverseContourPen

def replace_counters(font):
    glyphs=font.getGlyphSet()
    names={font.getBestCmap()[ord(ch)] for ch in 'оОoO'}
    for name in names:
        original=DecomposingRecordingPen(glyphs);glyphs[name].draw(original)
        contours=[];current=[]
        for op,args in original.value:
            current.append((op,args))
            if op in ('closePath','endPath'):
                rec=RecordingPen();rec.value=current
                area=AreaPen(glyphs);rec.replay(area)
                contours.append((abs(area.value),area.value,rec));current=[]
        _,outer_area,outer=max(contours,key=lambda row:row[0])
        bounds=BoundsPen(glyphs);outer.replay(bounds)
        x0,y0,x1,y1=bounds.bounds;w=x1-x0;h=y1-y0
        pen=TTGlyphPen(glyphs);outer.replay(pen)
        for fraction in (.34,.66):
            hole=pen if outer_area>0 else ReverseContourPen(pen)
            x=x0+w*fraction;y=(y0+y1)/2;rx=w*.065;ry=h*.23
            hole.moveTo((x-rx,y))
            hole.qCurveTo((x-rx,y+ry),(x,y+ry))
            hole.qCurveTo((x+rx,y+ry),(x+rx,y))
            hole.qCurveTo((x+rx,y-ry),(x,y-ry))
            hole.qCurveTo((x-rx,y-ry),(x-rx,y))
            hole.closePath()
        font['glyf'][name]=pen.glyph()
