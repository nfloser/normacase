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

  it('klappt erfüllte Zweige zu, offene auf, und kann alles auf- und zuklappen', () => {
    const leaf = (field: string, result: string) => `{"kind":"field_equals","result":"${result}","field":"${field}","expected":{"kind":"TRUTH","truth":"YES"},
      "actual":{"kind":"TRUTH","truth":"${result === 'MATCHED' ? 'YES' : 'NO'}"},"minimum":null,"maximum":null,"children":[],"evidenceRequirementId":null,"evidenceStatus":null,"numericExpression":null}`;
    const group = (result: string, children: string[]) => `{"kind":"all","result":"${result}","field":null,"expected":null,"actual":null,"minimum":null,"maximum":null,
      "children":[${children.join(',')}],"evidenceRequirementId":null,"evidenceStatus":null,"numericExpression":null}`;
    const doc = `{"formatVersion":2,"platformVersion":"t","assessment":{"knowledgeRelease":"r","assessmentDate":"2026-10-10","outcome":"NOT_SUPPORTED","missingRequiredFields":[],
      "ruleTrace":{"ruleId":"R","ruleVersion":1,"sourceId":"S","conditionResult":"NOT_MATCHED","outcome":"NOT_SUPPORTED",
        "condition":${group('NOT_MATCHED', [group('MATCHED', [leaf('a', 'MATCHED')]), group('NOT_MATCHED', [leaf('b', 'NOT_MATCHED')])])},
        "source":{"id":"S","title":"Quelle"}},"domainOutputs":[]}}`;
    TestBed.configureTestingModule({ imports: [DecisionTrace] });
    const fixture = TestBed.createComponent(DecisionTrace);
    fixture.componentRef.setInput('assessment', readAssessment(doc)!);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const visible = () => Array.from(el.querySelectorAll('.cond-text')).map((t) => t.textContent!.split(':')[0]);
    expect(visible()).toEqual(['Alle folgenden Bedingungen', 'Alle folgenden Bedingungen', 'Alle folgenden Bedingungen', 'b']);

    Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent === 'Alle aufklappen')!.click();
    fixture.detectChanges();
    expect(visible()).toContain('a');
    Array.from(el.querySelectorAll<HTMLButtonElement>('button')).find((b) => b.textContent === 'Alle zuklappen')!.click();
    fixture.detectChanges();
    expect(visible()).toEqual(['Alle folgenden Bedingungen']);
    const toggle = el.querySelector<HTMLButtonElement>('.tree-toggle')!;
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    toggle.click();
    fixture.detectChanges();
    expect(visible().length).toBe(3);
  });
});
