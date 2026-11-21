import { TestBed } from '@angular/core/testing';
import { readAssessment } from './assessment';
import { DecisionTrace } from './decision-trace';

describe('DecisionTrace', () => {
  it('zeigt bei berechneten Zahlenwerten die Schwelle statt eines leeren Feldes', () => {
    const doc = `{"formatVersion":2,"platformVersion":"t","assessment":{"knowledgeRelease":"r","assessmentDate":"2026-10-10","outcome":"SUPPORTED","missingRequiredFields":[],
      "ruleTrace":{"ruleId":"R","ruleVersion":1,"sourceId":"S","conditionResult":"MATCHED","outcome":"SUPPORTED",
        "condition":{"kind":"number_gte","result":"MATCHED","field":null,"expected":{"kind":"NUMBER","number":12.5},"actual":{"kind":"NUMBER","number":15.00},
          "minimum":null,"maximum":null,"children":[],"evidenceRequirementId":null,"evidenceStatus":null,"numericExpression":null},
        "source":{"id":"S","title":"Quelle"}},"domainOutputs":[]}}`;
    TestBed.configureTestingModule({ imports: [DecisionTrace] });
    const fixture = TestBed.createComponent(DecisionTrace);
    fixture.componentRef.setInput('assessment', readAssessment(doc)!);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).querySelector('.cond-text')!.textContent!;
    expect(text).toBe('Zahlenwert: mindestens 12,5, vorliegend 15,00');
  });
});
