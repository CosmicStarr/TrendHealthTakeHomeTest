import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('renders the heading, the form and the list, and loads the first page', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('h1')?.textContent).toContain('Patient reminders');
    expect(el.querySelector('app-reminder-form form')).toBeTruthy();
    expect(el.querySelector('app-reminder-list table')).toBeTruthy();

    const req = TestBed.inject(HttpTestingController).expectOne((r) => r.url === '/reminders');
    expect(req.request.params.get('page')).toBe('1');
  });
});
