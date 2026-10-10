"""Offline fixture authoring. NCF1 is OUR controlled export, never an MD/OCR parser."""
from pathlib import Path
from decimal import Decimal
from hashlib import sha256
import json
import re
import subprocess
from xml.sax.saxutils import escape
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak
from reportlab.lib.styles import getSampleStyleSheet
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
for name,file in [('NCRegular','DejaVuSans.ttf'),('NCBold','DejaVuSans-Bold.ttf'),('NCItalic','DejaVuSans.ttf'),('NCBoldItalic','DejaVuSans-Bold.ttf')]:
    pdfmetrics.registerFont(TTFont(name,'/usr/share/fonts/truetype/dejavu/'+file))
pdfmetrics.registerFontFamily('NCRegular',normal='NCRegular',bold='NCBold',italic='NCItalic',boldItalic='NCBoldItalic')
from pypdf import PdfReader
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'examples/document-cases'
PREVIEW_CACHE={(c['caseId'],d['id']):d for c in json.loads((OUT/'catalog.json').read_text()).get('cases',[]) for d in c['documents']} if (OUT/'catalog.json').exists() else {}
BUNDLES=json.loads((ROOT/'scripts/clinical-case-bundles.de.json').read_text())
CONTEXTS=json.loads((ROOT/'scripts/document-case-context.de.json').read_text())
SOURCES = [
 {'id':'md-teaching-care','title':'MD Bund – Die Selbstständigkeit als Maß der Pflegebedürftigkeit, Lehrbeispiele','version':'Broschüre 2019, Tabellen S. 19 und 21','url':'https://md-bund.de/fileadmin/dokumente/Publikationen/SPV/Begutachtungsgrundlagen/19-05-20_NBI_Pflegebeduerftigkeit_Fach-Info_22_12_16.pdf','scope':'Öffentliche Lehrbeispiele, keine echten Patientenakten. Ergänzende Dokumente sind synthetisch.'},
 {'id':'dguv-accident','title':'DGUV Gutachtenauftrag A 2206 und D-Arztbericht F 1000','version':'A 2206 Stand 10/2025; F 1000 Stand 07/2018','url':'https://www.dguv.de/formtexte/aerzte/index.jsp','scope':'Originale öffentlich verlinkt; eigene synthetische Berichte. Keine Anerkennungs- oder MdE-Prüfung.'},
 {'id':'kbv-transfer','title':'KBV PIO Überleitungsbogen – öffentliche fiktive Fallbeispiele','version':'Phase I, Version 1.0.0; Recherche 10.10.2026','url':'https://hub.kbv.de/pages/viewpage.action?navigatingVersions=true&pageId=117506312','scope':'Überleitung Krankenhaus/Pflege; eigene synthetische Akte, keine Schnittstellenkonformität.'},
 {'id':'kbv-transport','title':'KBV PraxisInfo Krankenbeförderung','version':'Januar 2025','url':'https://www.kbv.de/documents/infothek/publikationen/praxisinfo/praxisinfo-krankenbefoerderung.pdf','scope':'Formular 4, Grund der Beförderung, Beförderungsmittel und Begründung; kein vollständiger Leistungsentscheid.'},
 {'id':'md-pflege','title':'MD Bund Begutachtungs-Richtlinien Pflege','version':'26.08.2026, in Kraft 01.10.2026','url':'https://md-bund.de/fileadmin/dokumente/Publikationen/SPV/Begutachtungsgrundlagen/BRi_Pflege_26_08_2026.pdf','scope':'Bereits festgestellte Modulsummen, Abschnitt 5.10.1. Keine Bewertung aus Freitext und keine Pflegegradentscheidung.'},
 {'id':'kbv-reha','title':'KBV Medizinische Rehabilitation','version':'Recherche 10.10.2026','url':'https://www.kbv.de/praxis/verordnungen/rehabilitation','scope':'Verordnung auf Formular 61; eigene Demonstration von Angaben und Anlagen, kein Originalformular.'},
 {'id':'md-hilfsmittel','title':'MD Bund Unterlagen für Hilfsmittelversorgungen','version':'12.03.2026 (Titel im PDF)','url':'https://www.medizinischerdienst.de/fileadmin/MD-zentraler-Ordner/Downloads/22_KK_Unterlagen/Checklisten_KK_251107/Checklisten_fuer_KK_Unterlagen_MD_Hilfsmittel_2026-03-31.pdf','scope':'Anlassbezogene Fallvorbereitung und Unterlagenanforderung, keine allgemeine Layoutvorgabe.'},
 {'id':'md-car-t','title':'Arztfragebogen zur Therapie mit CAR-T-Zellen','version':'31.10.2023 (Titel im PDF)','url':'https://www.medizinischerdienst.de/fileadmin/MD-zentraler-Ordner/Downloads/22_KK_Unterlagen/Checkliste_CAR-T-Zellen_2023-11-07.pdf','scope':'Diagnose, Verlauf, Befunde und Board-Empfehlung; nur Dokumentdemonstration, keine Therapiebewertung.'},
 {'id':'md-cannabinoid','title':'MD Bayern Arztfragebogen zu Cannabinoiden','version':'10.01.2024','url':'https://www.md-bayern.de/fileadmin/MD-Bayern/PDF/Arztfragebogen_Cannabinoide.pdf','scope':'Anamnese, bisherige Therapie, Alternativen und Behandlungsziel; zunächst Recherchegrundlage ohne fachliche Implementierung.'}
]
LABELS={'request_complete':'Auftragsangaben vollständig','criteria_confirmed':'Fiktives Demokriterium bestätigt', 'ambulatory_treatment':'Ambulante Behandlung','strict_medical_necessity':'Medizinische Notwendigkeit ausdrücklich bestätigt','disability_marker_ag':'Merkzeichen aG','disability_marker_bl':'Merkzeichen Bl','disability_marker_h':'Merkzeichen H','care_grade':'Pflegegrad laut Bescheid','care_grade_3_permanent_mobility_transport_need':'Dauerhafte Mobilitätsbeeinträchtigung bei Pflegegrad 3','care_level_2_on_2016_12_31':'Pflegestufe 2 am 31.12.2016','care_grade_3_or_higher_since_2017_01_01':'Pflegegrad mindestens 3 seit 01.01.2017'}
for i, label in enumerate(['Mobilität','Kognition und Kommunikation','Verhalten und psychische Problemlagen','Selbstversorgung','Krankheit und Therapie','Alltagsleben und Kontakte'],1): LABELS[f'module_{i}_sum']=f'Modul {i}: {label}'
DISPLAY={'YES':'Ja','NO':'Nein','UNKNOWN':'Nicht angegeben','NOT_APPLICABLE':'Nicht anwendbar'}

def extract(content, document_id, allowed, page=1):
    lines=content.splitlines()
    if 'NCF1' not in lines or 'END-NCF1' not in lines: raise ValueError('unsupported document format')
    start=lines.index('NCF1')+1
    result=[]; seen=set()
    for line in lines[start:]:
        if line=='END-NCF1': break
        if '=' not in line: continue
        key,value=line.split('=',1)
        if key not in allowed or key in seen: raise ValueError('unknown or duplicate field')
        if value not in DISPLAY and not re.fullmatch(r'-?(0|[1-9][0-9]*)(\.[0-9]+)?',value): raise ValueError('invalid value')
        seen.add(key); result.append({'id':document_id,'page':page,'field':key,'value':value,'method':'CONTROLLED_TEXT'})
    return result

def normalize(observations):
    grouped={}
    for o in observations: grouped.setdefault(o['field'],set()).add(o['value'])
    facts={}
    for key,values in grouped.items():
        value=next(iter(values)) if len(values)==1 else 'UNKNOWN'
        if value=='UNKNOWN': facts[key]={'kind':'UNKNOWN'}
        elif value in DISPLAY: facts[key]={'kind':'TRUTH','truth':value}
        else: facts[key]={'kind':'NUMBER','number':Decimal(value)}
    return {'formatVersion':1,'assessmentDate':'2026-10-10','facts':facts,'evidence':{}}

def write_json(path,value):
    # Fixtures use small exact integer/half values. No arbitrary-precision case input rounding.
    def encode(item):
        if isinstance(item, Decimal):
            if not item.is_finite(): raise ValueError('nonfinite decimal')
            return format(item, 'f')
        if isinstance(item, dict): return '{'+','.join(json.dumps(k,ensure_ascii=False)+':'+encode(v) for k,v in item.items())+'}'
        if isinstance(item, list): return '['+','.join(encode(v) for v in item)+']'
        return json.dumps(item,ensure_ascii=False)
    path.write_text(encode(value)+'\n')

def clinical_pdf(path, case_id, title, values, source_id, narrative, authored=None):
    profile=BUNDLES[case_id]; authored=authored or {}
    styles=getSampleStyleSheet()
    for style in styles.byName.values():
        style.fontName='NCBold' if 'Heading' in style.name or style.name=='Title' else 'NCRegular'
    styles['Normal'].fontSize=10; styles['Normal'].leading=15
    styles['Title'].fontSize=19; styles['Title'].leading=24; styles['Title'].textColor=colors.HexColor('#123c63')
    styles['Heading2'].fontSize=12; styles['Heading2'].textColor=colors.HexColor('#123c63')
    styles['Heading2'].spaceBefore=12
    title=authored.get('title',title)
    date=authored.get('date',profile['primaryDate']); issuer=authored.get('issuer',profile['primaryIssuer'])
    story=[Paragraph('Schulungsakte - Identität und Berichte synthetisch',styles['Normal']),Spacer(1,12),
           Paragraph(escape(issuer),styles['Heading2']),Paragraph(escape(title),styles['Title']),
           Paragraph(escape(profile['caseNumber'])+' | '+escape(profile['person'])+' | '+str(profile['age'])+' Jahre | '+escape(date),styles['Normal']),Spacer(1,12)]
    if narrative: story.extend([Paragraph(escape(narrative).replace('\n\n','<br/><br/>'),styles['Normal']),Spacer(1,8)])
    for section in authored.get('sections',[]):
        story.extend([Paragraph(escape(section['heading']),styles['Heading2']),Paragraph(escape(section['text']),styles['Normal'])])
    if values:
        story.append(Paragraph('Dokumentierte Angaben',styles['Heading2']))
        rows=[[Paragraph('Angabe',styles['Normal']),Paragraph('Wert',styles['Normal'])]]
        for key,value in values.items():
            rows.append([Paragraph(escape(LABELS.get(key,key)),styles['Normal']),Paragraph(escape(DISPLAY.get(value,value)),styles['Normal'])])
        table=Table(rows,colWidths=[305,175],repeatRows=1)
        table.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('BACKGROUND',(0,0),(-1,0),colors.HexColor('#eaf3f9')),('LINEBELOW',(0,0),(-1,-1),.4,colors.HexColor('#d0dbe5')),('LEFTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6)]))
        story.append(table)
    source=next(item for item in SOURCES if item['id']==source_id)
    story.extend([Spacer(1,14),Paragraph('Strukturbezug: '+escape(source['title'])+'; '+escape(source['version'])+'. Eigenes Berichtslayout, kein amtliches Originalformular.',styles['Normal'])])
    if values:
        story.extend([PageBreak(),Paragraph('Technische Übermittlungsanlage',styles['Heading1']),Paragraph('Kontrollierter Textadapter zur reproduzierbaren Testextraktion. Kein fachliches Dokumentenverständnis und kein institutionelles Austauschformat.',styles['Normal'])])
        for line in ['NCF1']+[key+'='+value for key,value in values.items()]+['END-NCF1']:
            story.append(Paragraph(escape(line),styles['Code']))
    def footer(canvas,doc):
        canvas.setStrokeColor(colors.HexColor('#256da8')); canvas.line(55,43,540,43)
        canvas.setFont('NCRegular',8); canvas.drawString(55,30,profile['caseNumber']+' | '+profile['person']); canvas.drawRightString(540,30,'Seite '+str(doc.page))
    SimpleDocTemplate(str(path),pagesize=A4,rightMargin=55,leftMargin=55,topMargin=40,bottomMargin=60,invariant=1).build(story,onFirstPage=footer,onLaterPages=footer)

def pdf(path, case_id, title, values, source_id, narrative):
    styles=getSampleStyleSheet(); story=[]
    for style in styles.byName.values(): style.fontName='NCBold' if 'Heading' in style.name or style.name=='Title' else 'NCRegular'
    clinical=case_id in {c['caseId'] for c in json.loads((ROOT/'scripts/source-backed-cases.de.json').read_text())}
    story += [Paragraph('Synthetischer Schulungsfall | Keine echte Patientenakte' if clinical else 'SYNTHETISCHE DEMO - KEINE ECHTE PATIENTENAKTE',styles['Normal'] if clinical else styles['Heading2']),Paragraph(escape(title),styles['Title']),Paragraph('Fiktive Einrichtung / Testperson '+escape(case_id)+' / Datum 10.10.2026',styles['Normal']),Spacer(1,18),Paragraph(escape(narrative).replace('\n\n','<br/><br/>'),styles['Normal']),Spacer(1,18)]
    rows=[[Paragraph('Angabe',styles['Heading3']),Paragraph('Dokumentierter Wert',styles['Heading3'])]]
    for k,v in values.items(): rows.append([Paragraph(escape(LABELS.get(k,k)),styles['Normal']),Paragraph(escape(DISPLAY.get(v,v)),styles['Normal'])])
    table=Table(rows,colWidths=[285,185]);table.setStyle(TableStyle([('VALIGN',(0,0),(-1,-1),'TOP'),('GRID',(0,0),(-1,-1),.5,colors.HexColor('#65756d')),('BACKGROUND',(0,0),(-1,0),colors.HexColor('#e9efeb')),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),8),('BOTTOMPADDING',(0,0),(-1,-1),8)]));story.append(table)
    story += [Spacer(1,18),Paragraph('Quelle der Dokumentstruktur: '+source_id+'. Eigenes Layout; keine Kopie eines amtlichen Formulars. Alle Angaben sind erfunden.',styles['Normal']),Paragraph('Keine echte Unterschrift, keine medizinische Empfehlung.',styles['Normal']),PageBreak(),Paragraph('Strukturierte Demo-Übermittlung',styles['Heading1']),Paragraph('Dieser kontrollierte Textabschnitt dient ausschließlich dem Testadapter. Er ist kein MD-Austauschformat und keine freie Dokumenterkennung.',styles['Normal'])]
    for line in ['NCF1']+[k+'='+v for k,v in values.items()]+['END-NCF1']: story.append(Paragraph(escape(line),styles['Code']))
    def footer(canvas,doc):
        canvas.setFont('NCRegular',9);canvas.drawString(55,30,'Synthetisch | '+case_id+' | Seite '+str(doc.page))
    SimpleDocTemplate(str(path),pagesize=A4,rightMargin=55,leftMargin=55,topMargin=45,bottomMargin=55,invariant=1).build(story,onFirstPage=footer,onLaterPages=footer)

def build_case(case_id,title,pack_path,source_id,parts,evidence,scope,expected=None):
    directory=OUT/case_id;directory.mkdir(parents=True,exist_ok=True)
    documents=[];observations=[]
    context=CONTEXTS.get(case_id)
    profile=BUNDLES.get(case_id)
    if profile and context: context={**context,'background':profile['background']}
    authored_documents={}
    if profile:
        title=profile['title']
        parts=list(parts)
        for index,part in enumerate(parts,1):
            authored=profile.get('overrides',{}).get(str(index),{})
            clean_title=authored.get('title',part[0]).replace(' – synthetischer Bericht','').replace(' – ergänzter Schulungsbericht','').replace(' - synthetischer Nachweis','').replace(' – synthetische Anlage','').replace(' – synthetische Anfrage','').replace(' – synthetische Begleitunterlage','')
            parts[index-1]=(clean_title,part[1],part[2] if not authored else '')
            authored_documents[index]=authored
        for extra in profile['documents']:
            parts.append((extra['title'],{},''));authored_documents[len(parts)]=extra
    for index,(doc_title,values,narrative) in enumerate(parts,1):
        doc_id=f'document-{index}';path=directory/(doc_id+'.pdf')
        if context and index==1: narrative=context['background']+' '+narrative
        if profile: clinical_pdf(path,case_id,doc_title,values,source_id,narrative,authored_documents.get(index))
        else: pdf(path,case_id,doc_title,values,source_id,narrative)
        reader=PdfReader(path)
        for page_no,page in enumerate(reader.pages,1):
            content=page.extract_text()
            if 'NCF1' in content.splitlines(): observations+=extract(content,doc_id,set(values),page_no)
        cached=PREVIEW_CACHE.get((case_id,doc_id),{})
        previews=cached.get('previews',[])
        if cached.get('sha256')!=sha256(path.read_bytes()).hexdigest() or len(previews)!=len(reader.pages) or any(not (directory/p['filename']).exists() or sha256((directory/p['filename']).read_bytes()).hexdigest()!=p['sha256'] for p in previews):
            subprocess.run(['pdftoppm','-r','100','-png',str(path),str(directory/(doc_id+'-page'))],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
            previews=[]
            for page_no in range(1,len(reader.pages)+1):
                preview=directory/(doc_id+'-page-'+str(page_no)+'.png')
                previews.append({'page':page_no,'filename':preview.name,'sha256':sha256(preview.read_bytes()).hexdigest()})
        retained_previews={preview['filename'] for preview in previews}
        for stale in directory.glob(doc_id+'-page-*.png'):
            if stale.name not in retained_previews: stale.unlink()
        documents.append({'previews':previews,'id':doc_id,'title':doc_title,'filename':path.name,'mediaType':'application/pdf','sha256':sha256(path.read_bytes()).hexdigest(),'pages':len(reader.pages),'sourceId':source_id})
    # A raster attachment intentionally has no automatic extraction; the PDF remains the text source.
    if parts:
        path=directory/'scan.png'; image=Image.new('RGB',(1000,1350),'#f4f3ef');draw=ImageDraw.Draw(image);font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',22)
        lines=([profile['caseNumber'],profile['person'],'Anlagenkopie: '+parts[0][0],'Schulungsakte - synthetische Identität'] if profile else ['SYNTHETISCHE SCAN-DEMO',case_id,parts[0][0],''])+[LABELS.get(k,k)+': '+DISPLAY.get(v,v) for k,v in parts[0][1].items()]+['','Keine OCR-Auswertung.','Textquelle: zugehöriges PDF.']
        for i,line in enumerate(lines): draw.text((55,70+i*55),line,font=font,fill='#182d25')
        image.save(path);documents.append({'id':'scan','title':'Scan-Anlage (ohne automatische Texterkennung)','filename':path.name,'mediaType':'image/png','sha256':sha256(path.read_bytes()).hexdigest(),'pages':1,'sourceId':source_id})
    normalized=normalize(observations);normalized['evidence']=evidence
    path=directory/'transmission.txt'
    transcript=('Schulungsakte - '+profile['caseNumber']+' | '+profile['person'] if profile else 'SYNTHETISCHE DEMO - '+case_id)+'\nKontrollierter PDF-Text, keine OCR.\n\n'
    for doc in documents:
        if doc['mediaType']=='application/pdf':
            reader=PdfReader(directory/doc['filename'])
            for page_no,page in enumerate(reader.pages,1): transcript+=doc['title']+' / Seite '+str(page_no)+'\n'+page.extract_text()+'\n'
    path.write_text(transcript)
    documents.append({'id':'transmission','title':'Lesefassung der Aktenunterlagen' if profile else 'Textübermittlung der Test-PDFs','filename':path.name,'mediaType':'text/plain','sha256':sha256(path.read_bytes()).hexdigest(),'pages':1,'sourceId':source_id})
    findings=[]
    for key in normalized['facts']:
        values={o['value'] for o in observations if o['field']==key}
        if len(values)>1: findings.append('Widersprüchliche Angaben: '+LABELS.get(key,key)+'. Der Wert bleibt unbekannt.')
        elif normalized['facts'][key]['kind']=='UNKNOWN': findings.append('Fehlende Angabe: '+LABELS.get(key,key)+'.')
    if evidence.get('supporting_document')=='MISSING': findings.append('Ein erforderlicher Nachweis fehlt. Dokumentvorhandensein ersetzt keine fachliche Bestätigung.')
    write_json(directory/'input.json',normalized)
    input_hash=sha256((directory/'input.json').read_bytes()).hexdigest()
    return {'caseId':case_id,'context':context,'title':title,'packPath':pack_path,'sourceId':source_id,'scope':scope,'expectedOutcome':expected,'inputSha256':input_hash,'documents':documents,'observations':observations,'findings':findings,'evidenceLabels':{'supporting_document':'Synthetischer Nachweis','severe_disability_card':'Schwerbehindertenausweis','care_grade_notice':'Pflegegradbescheid','care_transition_classification_proof':'Nachweis der Überleitung'},'outputLabels':({'approval_state':{'label':'Genehmigungsfiktion nach diesem Referenzpfad','choices':{'DEEMED_GRANTED':'Nach diesem Referenzpfad als erteilt anzusehen','SECTION_8_3_DEEMING_RULE_NOT_APPLICABLE':'Dieser Referenzpfad ist nicht anwendbar'}}} if pack_path=='kt-rl-8-3' else {f'score_threshold_{t}':{'label':'Score-Schwelle '+t.replace('_',','),'choices':{'REACHED':'Erreicht','NOT_REACHED':'Nicht erreicht'}} for t in ['12_5','27','47_5','70','90']} if pack_path=='pflege-adult-score' else {}),'fieldLabels':{o['field']:LABELS.get(o['field'],o['field']) for o in observations}}

def generate():
    cases=[]
    for variant,count in [('supported',30),('not-supported',30),('incomplete',20),('review',15),('technical',5)]:
        ids=(['demo-technical'] if variant=='technical' else ['demo-g-'+variant])+[f'demo-volume-{variant}-{i:02}' for i in range(1,count)]
        for case_id in ids:
            if variant=='technical':
                parts=[('Übermittlungsprotokoll',{},'Der synthetische Eingang ist technisch ungültig. Es wurde keine fachliche Bewertung erzeugt.')];evidence={}
            else:
                complete='UNKNOWN' if variant=='incomplete' else 'YES';confirmed='NO' if variant=='not-supported' else 'YES'
                parts=[('Prüfauftrag und Anlagenübersicht',{'request_complete':complete},'Auftrag zur regelbasierten Demonstrationsprüfung. Diese Kriterien sind fiktiv und keine medizinische Regel.')]
                if variant!='review':parts.append(('Bestätigung des fiktiven Prüfkriteriums',{'criteria_confirmed':confirmed},'Fiktiver Nachweis. Die dokumentierte Bestätigung ist ausschließlich für die Demonstration bestimmt.'))
                else:parts[0][1]['criteria_confirmed']=confirmed
                evidence={'supporting_document':'MISSING' if variant=='review' else 'PRESENT'}
            case=build_case(case_id,'Synthetischer Arbeitslistenfall',None if variant=='technical' else 'demo-g','md-hilfsmittel',parts,evidence,'Fiktive Plattformregel; keine Hilfsmittelentscheidung.',{'supported':'SUPPORTED','not-supported':'NOT_SUPPORTED','incomplete':'INCOMPLETE','review':'HUMAN_REVIEW'}.get(variant))
            # Existing pitch fixtures retain their exact historical assessment date.
            file=OUT/case_id/'input.json';data=json.loads(file.read_text());data['assessmentDate']='2026-10-03';write_json(file,data);case['inputSha256']=sha256(file.read_bytes()).hexdigest();cases.append(case)
    base={'ambulatory_treatment':'YES','strict_medical_necessity':'YES','disability_marker_ag':'NO','disability_marker_bl':'NO','disability_marker_h':'NO','care_grade':'4','care_grade_3_permanent_mobility_transport_need':'NO','care_level_2_on_2016_12_31':'NO','care_grade_3_or_higher_since_2017_01_01':'NO'}
    for variant in ['complete','missing','conflicting','negative']:
        values=dict(base)
        if variant=='missing':values['strict_medical_necessity']='UNKNOWN'
        if variant=='negative':
            values['care_grade']='2';values['strict_medical_necessity']='NO'
        parts=[('Krankenbeförderung - Angaben zur Verordnung',values,'Krankenfahrt mit Taxi/Mietwagen zur ambulanten Behandlung. Kein Krankentransportwagen. Eigene Testvorlage mit Inhalten angelehnt an Muster 4.')]
        parts.append(('Pflegekassenbescheid - synthetischer Nachweis',{'care_grade':'3' if variant=='conflicting' else values['care_grade']},'Erfundener Bescheid zum Nachweis des dokumentierten Pflegegrades. Kein realer Leistungsbescheid.'))
        cases.append(build_case('reference-transport-'+variant,'Krankenfahrt: '+{'complete':'vollständig','missing':'Pflichtangabe fehlt','conflicting':'widersprüchliche Nachweise','negative':'Referenzpfad nicht erfüllt'}[variant],'kt-rl-8-3','kbv-transport',parts,{'care_grade_notice':'CONFLICTING' if variant=='conflicting' else 'PRESENT','severe_disability_card':'MISSING','care_transition_classification_proof':'MISSING'},'Nur KT-RL § 8 Abs. 3: keine vollständige Kostenübernahmeprüfung.',{'complete':'SUPPORTED','missing':'INCOMPLETE','conflicting':'HUMAN_REVIEW','negative':'NOT_SUPPORTED'}[variant]))
    for variant,values in [('complete',['2','4','1','8','2','3']),('missing',['2','4','1','UNKNOWN','2','3']),('out-of-range',['16','4','1','8','2','3']),('conflicting',['2','4','1','8','2','3'])]:
        fields={f'module_{i}_sum':v for i,v in enumerate(values,1)}
        parts=[('Pflege - dokumentierte Modulsummen',fields,'Fiktive erwachsene Testperson. Die Modulsummen gelten als bereits fachlich festgestellt. Diese Demo ermittelt sie nicht aus Symptomen oder Diagnosen.')]
        if variant=='conflicting':parts.append(('Abweichender Modulnachweis',{'module_4_sum':'3'},'Widerspruch zur ersten Anlage. Kein automatisches Bevorzugen einer Quelle.'))
        cases.append(build_case('reference-care-'+variant,'Pflege-Score: '+{'complete':'vollständig','missing':'Modulsumme fehlt','out-of-range':'Wert außerhalb des Bereichs','conflicting':'widersprüchliche Modulsummen'}[variant],'pflege-adult-score','md-pflege',parts,{},'Nur mathematische Gewichtung festgestellter Modulsummen. Keine Pflegegradentscheidung.',{'complete':'SUPPORTED','missing':'INCOMPLETE','out-of-range':'HUMAN_REVIEW','conflicting':'INCOMPLETE'}[variant]))
    LABELS.update({'reha_goal':'Rehabilitationsziel dokumentiert','functional_limitation':'Funktionseinschränkungen beschrieben','findings_attached':'Befundbericht beigefügt','diagnosis_documented':'Diagnose durch Bericht dokumentiert','history_documented':'Therapieverlauf dokumentiert','board_attached':'Board-Protokoll beigefügt'})
    for variant in ['complete','missing']:
        parts=[('Rehabilitation - Auftragsangaben',{'reha_goal':'YES','functional_limitation':'YES'},'Fiktive erwachsene Testperson mit eingeschränkter Gehstrecke nach orthopädischer Behandlung. Zielangabe: selbständige Alltagsmobilität. Dies sind erfundene Fallangaben, keine ärztliche Empfehlung.')]
        if variant=='complete': parts.append(('Ärztlicher Befundbericht',{'findings_attached':'YES'},'Fiktiver Verlauf: Belastbarkeit eingeschränkt, Gehstrecke 200 Meter laut Testangabe. Befund und Ziel sind für die Unterlagensichtung dokumentiert. Keine Bewertung einer Rehabilitationsindikation.'))
        else: parts[0][1]['findings_attached']='UNKNOWN'
        cases.append(build_case('reference-reha-'+variant,'Rehabilitation: '+('Unterlagen vorhanden' if variant=='complete' else 'Befundbericht fehlt'),None,'kbv-reha',parts,{},'Nur Unterlagensichtung angelehnt an Formular 61. Keine fachlich freigegebene Rehabilitationsprüfung.'))
        parts=[('Onkologie - Zusammenstellung der Unterlagen',{'diagnosis_documented':'YES','history_documented':'YES'},'Fiktive Testperson mit dokumentierter Lymphomerkrankung. Histologischer Bericht und mehrstufiger Therapieverlauf sind in der Aktenübersicht benannt. Präparat und Nutzen-Risiko-Bewertung werden hier nicht festgelegt.')]
        if variant=='complete': parts.append(('Board-Protokoll - synthetische Anlage',{'board_attached':'YES'},'Fiktive interdisziplinäre Fallbesprechung. Vorbefunde und bisheriger Verlauf sollen fachlich geprüft werden. Kein verbindlicher Therapieentscheid und keine Empfehlung eines Präparats.'))
        else:parts[0][1]['board_attached']='UNKNOWN'
        cases.append(build_case('reference-oncology-'+variant,'Onkologie: '+('Unterlagen vorhanden' if variant=='complete' else 'Board-Protokoll fehlt'),None,'md-car-t',parts,{},'Nur Dokumentdemonstration auf Basis der veröffentlichten CAR-T-Checkliste. Keine Therapiebewertung.'))
    LABELS.update({'accident_event_documented':'Unfallereignis dokumentiert','initial_findings_attached':'Erstbefund beigefügt','transfer_report_attached':'Überleitungsbericht beigefügt','medication_overview_attached':'Medikationsübersicht beigefügt','treatment_goal_documented':'Therapieziel dokumentiert','previous_treatments_attached':'Vorbehandlungen belegt','prescription_attached':'Verordnung beigefügt','measurement_attached':'Messbefund beigefügt','fitting_report_attached':'Anpassbericht beigefügt'})
    for authored in json.loads((ROOT/'scripts/source-backed-cases.de.json').read_text()):
        parts=[(d['title'],d['values'],d['narrative']) for d in authored['documents']]
        cases.append(build_case(authored['caseId'],authored['title'],authored['packPath'],authored['sourceId'],parts,{},authored['scope'],authored['expectedOutcome']))
    write_json(OUT/'catalog.json' ,{'formatVersion':1,'sources':SOURCES,'cases':cases})

if __name__=='__main__': generate()
