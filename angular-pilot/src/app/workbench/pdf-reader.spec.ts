import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PdfReader } from './pdf-reader';
import { DemoDocument } from './document-case';

const docs: DemoDocument[] = [
  { id: 'document-1', title: 'Arztbrief', mediaType: 'application/pdf', pages: 3, previewPages: [1, 2, 3] },
  { id: 'scan', title: 'Scan', mediaType: 'image/png', pages: 1, previewPages: [] },
  { id: 'transmission', title: 'Text', mediaType: 'text/plain', pages: 1, previewPages: [] },
];

async function reader() {
  await TestBed.configureTestingModule({ imports: [PdfReader], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
  const fixture = TestBed.createComponent(PdfReader);
  fixture.componentRef.setInput('caseId', 'reference-a');
  fixture.componentRef.setInput('documents', docs);
  fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement, http: TestBed.inject(HttpTestingController) };
}
const sheets = (e: HTMLElement) => [...e.querySelectorAll<HTMLElement>('.sheet')];

describe('Dokumentleser', () => {
  it('shows every preview page of the first PDF and pages with the toolbar', async () => {
    const { fixture, element } = await reader();
    expect(sheets(element).length).toBe(3);
    expect(element.querySelector('img')!.getAttribute('src')).toBe('/api/document-cases/reference-a/documents/document-1/pages/1');
    const input = element.querySelector<HTMLInputElement>('.page-field input')!;
    expect(input.value).toBe('1');
    element.querySelector<HTMLButtonElement>('[aria-label="Nächste Seite"]')!.click(); fixture.detectChanges();
    expect(input.value).toBe('2');
    input.value = '9'; input.dispatchEvent(new Event('input')); input.dispatchEvent(new Event('change')); fixture.detectChanges();
    expect(input.value).toBe('3');
    expect(element.querySelector('a[href$="?download=true"]')).toBeTruthy();
  });
  it('rotates pages by swapping the laid-out box', async () => {
    const { fixture, element } = await reader();
    const before = sheets(element)[0];
    const [w, h] = [parseFloat(before.style.width), parseFloat(before.style.height)];
    element.querySelector<HTMLButtonElement>('[aria-label="Drehen (R)"]')!.click(); fixture.detectChanges();
    const after = sheets(element)[0];
    // Fit to width keeps the outer width; the turned page becomes landscape.
    expect(parseFloat(after.style.width)).toBeCloseTo(w, 0);
    expect(parseFloat(after.style.height)).toBeCloseTo((w * w) / h, 0);
    expect(after.querySelector('img')!.style.transform).toContain('rotate(90deg)');
  });
  it('zooms in steps and back to page width', async () => {
    const { fixture, element } = await reader();
    const width = () => parseFloat(sheets(element)[0].style.width);
    const fit = width();
    element.querySelector<HTMLButtonElement>('[aria-label="Vergrößern"]')!.click(); fixture.detectChanges();
    expect(width()).toBeCloseTo(fit * 1.25, 0);
    const stage = element.querySelector<HTMLElement>('.reader-stage')!;
    stage.dispatchEvent(new KeyboardEvent('keydown', { key: '0' })); fixture.detectChanges();
    expect(width()).toBeCloseTo(fit, 0);
  });
  it('jumps to the page of a provenance link and marks it', async () => {
    const { fixture, element } = await reader();
    fixture.componentRef.setInput('target', { documentId: 'document-1', page: 2, label: 'Pflegegrad', token: 1 }); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector<HTMLInputElement>('.page-field input')!.value).toBe('2');
    expect(element.querySelector('.sheet.highlighted')!.getAttribute('data-page')).toBe('2');
    expect(element.querySelector('.jump-chip')!.textContent).toContain('Pflegegrad');
  });
  it('shows scans as one page and loads text documents', async () => {
    const { fixture, element, http } = await reader();
    [...element.querySelectorAll<HTMLButtonElement>('.file-item')][1].click(); fixture.detectChanges();
    expect(sheets(element).length).toBe(1);
    expect(element.querySelector('img')!.getAttribute('src')).toBe('/api/document-cases/reference-a/documents/scan');
    [...element.querySelectorAll<HTMLButtonElement>('.file-item')][2].click(); fixture.detectChanges();
    http.expectOne('/api/document-cases/reference-a/documents/transmission').flush('Kontrollierter Text'); fixture.detectChanges();
    expect(element.querySelector('.reader-text')!.textContent).toBe('Kontrollierter Text');
    http.verify();
  });
});
