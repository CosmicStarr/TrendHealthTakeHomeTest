import { Component, inject, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Recurrence, Reminder } from './reminder.models';
import { RemindersService, describeError } from './reminders.service';

/** `yyyy-MM-ddTHH:mm` in local time: the value format a datetime-local input uses. */
export function toLocalInputValue(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

function nextWholeHour(): Date {
  const d = new Date();
  d.setHours(d.getHours() + 1, 0, 0, 0);
  return d;
}

@Component({
  selector: 'app-reminder-form',
  imports: [ReactiveFormsModule],
  templateUrl: './reminder-form.html',
})
export class ReminderForm {
  private readonly api = inject(RemindersService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly created = output<Reminder>();

  readonly recurrences: Recurrence[] = ['None', 'Daily', 'Weekly'];

  readonly form = this.fb.group({
    patientName: ['', [Validators.required, Validators.maxLength(200)]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    notes: ['', [Validators.maxLength(2000)]],
    dueDate: [toLocalInputValue(nextWholeHour()), [Validators.required]],
    recurrence: ['None' as Recurrence],
  });

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly serverFieldErrors = signal<Record<string, string[]>>({});
  readonly lastCreated = signal<Reminder | null>(null);

  /** Show a control's errors once the user has left the field, or after they try to submit. */
  showErrors(name: 'patientName' | 'title' | 'notes' | 'dueDate'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || control.dirty);
  }

  submit(): void {
    this.error.set(null);
    this.serverFieldErrors.set({});
    this.lastCreated.set(null);

    // Treat whitespace-only values as empty, matching the API's [Required] behaviour.
    for (const name of ['patientName', 'title'] as const) {
      const control = this.form.controls[name];
      control.setValue(control.value.trim());
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.form.disable();

    this.api
      .create({
        patientName: value.patientName,
        title: value.title,
        notes: value.notes.trim(),
        // datetime-local is in local time with no offset; send it as UTC.
        dueDate: new Date(value.dueDate).toISOString(),
        recurrence: value.recurrence,
      })
      .subscribe({
        next: (reminder) => {
          this.submitting.set(false);
          this.form.enable();
          this.form.reset();
          this.form.controls.dueDate.setValue(toLocalInputValue(nextWholeHour()));
          this.lastCreated.set(reminder);
          this.created.emit(reminder);
        },
        error: (err) => {
          this.submitting.set(false);
          this.form.enable();
          const described = describeError(err);
          this.error.set(described.message);
          this.serverFieldErrors.set(described.fieldErrors);
        },
      });
  }

  serverErrorsFor(field: string): string[] {
    return this.serverFieldErrors()[field] ?? [];
  }
}
