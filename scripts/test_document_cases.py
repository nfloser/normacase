import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('fixtures', Path(__file__).with_name('generate_document_cases.py'))
fixtures = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fixtures)

class DocumentExtractionTests(unittest.TestCase):
    def test_missing_is_not_inferred(self):
        result = fixtures.normalize([{'id':'a','page':1,'field':'request_complete','value':'UNKNOWN'}])
        self.assertEqual(result['facts']['request_complete'], {'kind':'UNKNOWN'})
    def test_conflicting_observations_stay_unknown(self):
        result = fixtures.normalize([{'id':'a','page':1,'field':'criteria_confirmed','value':'YES'},
                                     {'id':'b','page':1,'field':'criteria_confirmed','value':'NO'}])
        self.assertEqual(result['facts']['criteria_confirmed'], {'kind':'UNKNOWN'})
    def test_invalid_values_and_duplicate_lines_fail(self):
        for content in ['NCF1\nrequest_complete=maybe\nEND-NCF1', 'NCF1\nrequest_complete=YES\nrequest_complete=NO\nEND-NCF1']:
            with self.assertRaises(ValueError): fixtures.extract(content, 'a', {'request_complete'})
    def test_scans_do_not_claim_automatic_extraction(self):
        with self.assertRaises(ValueError): fixtures.extract('image bytes', 'scan', {'request_complete'})
    def test_exact_decimal(self):
        result=fixtures.normalize([{'id':'a','page':1,'field':'module_1_sum','value':'2.5'}])
        self.assertEqual(str(result['facts']['module_1_sum']['number']), '2.5')



class RetainedCorpusTests(unittest.TestCase):
    def test_every_file_hash_and_pdf_observation_matches_retained_input(self):
        import json
        from hashlib import sha256
        from decimal import Decimal
        from pypdf import PdfReader
        root=fixtures.OUT
        catalog=json.loads((root/'catalog.json').read_text())
        self.assertEqual(len(catalog['cases']),122)
        for case in catalog['cases']:
            directory=root/case['caseId']
            input_bytes=(directory/'input.json').read_bytes()
            self.assertEqual(sha256(input_bytes).hexdigest(),case['inputSha256'])
            input=json.loads(input_bytes,parse_float=Decimal)
            observations=[]
            for document in case['documents']:
                path=directory/document['filename']
                self.assertEqual(sha256(path.read_bytes()).hexdigest(),document['sha256'])
                if document['mediaType']=='application/pdf':
                    reader=PdfReader(path)
                    self.assertEqual(len(reader.pages),document['pages'])
                    self.assertEqual(len(document['previews']),document['pages'])
                    for preview in document['previews']:
                        self.assertEqual(sha256((directory/preview['filename']).read_bytes()).hexdigest(),preview['sha256'])
                    for page_no,page in enumerate(reader.pages,1):
                        content=page.extract_text()
                        if 'NCF1' in content.splitlines():
                            observations+=fixtures.extract(content,document['id'],set(input['facts']),page_no)
            self.assertEqual(observations,case['observations'])
            self.assertEqual(fixtures.normalize(observations)['facts'],input['facts'])
            self.assertTrue(all(o['id']!='scan' for o in observations))

    def test_additional_cases_cover_public_teaching_cases_and_multiple_document_families(self):
        import json
        catalog=json.loads((fixtures.OUT/'catalog.json').read_text())
        cases={c['caseId']:c for c in catalog['cases']}
        for id in ['reference-md-mueller','reference-md-kraemer','reference-accident-complete','reference-accident-missing','reference-accident-conflicting','reference-transfer-complete','reference-transfer-missing','reference-cannabis-complete','reference-cannabis-missing','reference-aid-hearing']:
            self.assertIn(id,cases)
            case=cases[id]
            self.assertTrue(case['context']['question'])
            self.assertGreaterEqual(len(case['documents']),4)
            if not id.startswith('reference-md-'):
                self.assertIsNone(case['packPath'])
        self.assertEqual([o['value'] for o in cases['reference-md-mueller']['observations'][:6]],['0','11','3','15','2','6'])
        self.assertTrue(cases['reference-accident-conflicting']['findings'])

    def test_reference_context_is_retained_in_the_first_pdf(self):
        import json
        from pypdf import PdfReader
        catalog=json.loads((fixtures.OUT/'catalog.json').read_text())
        for case in catalog['cases']:
            context=case.get('context')
            if not case['caseId'].startswith('reference-'):
                self.assertIsNone(context)
                continue
            self.assertTrue(all(context[key] for key in ['request','question','background']))
            content=PdfReader(fixtures.OUT/case['caseId']/'document-1.pdf').pages[0].extract_text()
            self.assertIn(' '.join(context['background'].split()),' '.join(content.split()))

    def test_transport_complete_has_no_false_missing_evidence_warning(self):
        import json
        catalog=json.loads((fixtures.OUT/'catalog.json').read_text())
        case=next(c for c in catalog['cases'] if c['caseId']=='reference-transport-complete')
        self.assertEqual(case['findings'],[])
        missing=next(c for c in catalog['cases'] if c['caseId']=='reference-transport-missing')
        self.assertTrue(any('Medizinische Notwendigkeit' in f for f in missing['findings']))

    def test_reference_bundles_are_complete_and_do_not_change_assessment_inputs(self):
        import json
        from pypdf import PdfReader
        catalog=json.loads((fixtures.OUT/'catalog.json').read_text())
        bundles=json.loads((fixtures.ROOT/'scripts/clinical-case-bundles.de.json').read_text())
        baseline=json.loads((fixtures.ROOT/'scripts/reference-input-baseline.json').read_text())
        for case in catalog['cases']:
            if case['caseId'] not in bundles: continue
            profile=bundles[case['caseId']]
            self.assertEqual(case['inputSha256'],baseline[case['caseId']])
            pdfs=[d for d in case['documents'] if d['mediaType']=='application/pdf']
            self.assertGreaterEqual(len(pdfs),4)
            self.assertLessEqual(len(case['documents']),10)
            self.assertIn(profile['caseNumber'],case['title'])
            for document in pdfs:
                self.assertNotIn('Demo',document['title'])
                content=' '.join(PdfReader(fixtures.OUT/case['caseId']/document['filename']).pages[0].extract_text().split())
                self.assertIn(profile['person'],content)
                self.assertIn(profile['caseNumber'],content)
            for supplement in profile['documents']:
                document=next(d for d in pdfs if d['title']==supplement['title'])
                content=' '.join(p.extract_text() for p in PdfReader(fixtures.OUT/case['caseId']/document['filename']).pages)
                for section in supplement['sections']:
                    self.assertIn(section['heading'],content)
            self.assertTrue(all(o['id'].startswith('document-') for o in case['observations']))

    def test_retained_decimal_json_does_not_round(self):
        import tempfile,json
        from decimal import Decimal
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'input.json'; value=Decimal('1.1234567890123456789')
            fixtures.write_json(path,{'number':value})
            self.assertEqual(json.loads(path.read_text(),parse_float=Decimal)['number'],value)

if __name__=='__main__': unittest.main()
