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
        self.assertEqual(len(catalog['cases']),112)
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
                    for page_no,page in enumerate(reader.pages,1):
                        content=page.extract_text()
                        if 'NCF1' in content.splitlines():
                            observations+=fixtures.extract(content,document['id'],set(input['facts']),page_no)
            self.assertEqual(observations,case['observations'])
            self.assertEqual(fixtures.normalize(observations)['facts'],input['facts'])
            self.assertTrue(all(o['id']!='scan' for o in observations))

    def test_retained_decimal_json_does_not_round(self):
        import tempfile,json
        from decimal import Decimal
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'input.json'; value=Decimal('1.1234567890123456789')
            fixtures.write_json(path,{'number':value})
            self.assertEqual(json.loads(path.read_text(),parse_float=Decimal)['number'],value)

if __name__=='__main__': unittest.main()
