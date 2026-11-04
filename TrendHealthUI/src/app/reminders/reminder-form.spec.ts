import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReminderForm } from './reminder-form';
import { Reminder } from './reminder.models';

describe('ReminderForm', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReminderForm],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function setup() {
    const fixture = TestBed.createComponent(ReminderForm);
    fixture.detectChanges();
    return { fixture, form: fixture.componentInstance };
  }

  it('does not submit when required fields are blank or whitespace', () => {
    const { form } = setup();
    form.form.patchValue({ patientName: '   ', title: '' });

    form.submit();

    http.expectNone('/reminders');
    expect(form.form.controls.patientName.hasError('required')).toBe(true);
    expect(form.showErrors('title')).toBe(true);
  });

  it('posts a trimmed body with a UTC due date and emits the created reminder', () => {
    const { form } = setup();
    const emitted: Reminder[] = [];
    form.created.subscribe((r) => emitted.push(r));
    form.form.setValue({
      patientName: ' Jane Doe ',
      title: 'Check BP',
      notes: '',
      dueDate: '2026-11-05T09:00',
      recurrence: 'Weekly',
    });

    form.submit();
    expect(form.submitting()).toBe(true);

    const req = http.expectOne('/reminders');
    expect(req.request.body).toEqual({
      patientName: 'Jane Doe',
      title: 'Check BP',
      notes: '',
      dueDate: new Date('2026-11-05T09:00').toISOString(),
      recurrence: 'Weekly',
    });
    req.flush({ id: 7, title: 'Check BP' } as Reminder);

    expect(form.submitting()).toBe(false);
    expect(emitted.map((r) => r.id)).toEqual([7]);
    expect(form.form.controls.title.value).toBe('');
  });

  it('shows server validation errors next to the field', async () => {
    const { fixture, form } = setup();
    form.form.patchValue({ patientName: 'Jane', title: 'Check BP' });

    form.submit();
    http
      .expectOne('/reminders')
      .flush(
        {
          statusCode: 400,
          message: 'One or more validation errors occurred.',
          errors: { title: ['Too long'] },
        },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(form.error()).toBe('One or more validation errors occurred.');
    expect(text).toContain('Too long');
  });
});
