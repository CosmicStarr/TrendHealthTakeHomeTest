import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, of, switchMap, tap } from 'rxjs';
import {
  PagedResponse,
  Reminder,
  ReminderQuery,
  ReminderStatus,
  SortDirection,
  SortField,
} from './reminder.models';
import { RemindersService, describeError } from './reminders.service';

type SortOption = `${SortField}:${SortDirection}`;

@Component({
  selector: 'app-reminder-list',
  imports: [DatePipe],
  templateUrl: './reminder-list.html',
})
export class ReminderList implements OnInit {
  private readonly api = inject(RemindersService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload$ = new Subject<ReminderQuery>();

  readonly pageSize = 10;
  readonly status = signal<ReminderStatus | ''>('');
  readonly sort = signal<SortOption>('dueDate:asc');
  readonly page = signal(1);

  readonly result = signal<PagedResponse<Reminder> | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly highlightId = signal<number | null>(null);
  readonly completingId = signal<number | null>(null);

  ngOnInit(): void {
    // switchMap drops a response that's still in flight when a newer query starts,
    // so a slow, older response can't overwrite a newer one.
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap((query) =>
          this.api.list(query).pipe(
            catchError((err) => {
              this.error.set(describeError(err).message);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) {
          this.result.set(result);
        }
      });
    this.load();
  }

  load(): void {
    const [sortBy, sortDir] = this.sort().split(':') as [SortField, SortDirection];
    this.reload$.next({
      page: this.page(),
      pageSize: this.pageSize,
      status: this.status() || undefined,
      sortBy,
      sortDir,
    });
  }

  onStatusChange(value: string): void {
    this.status.set(value as ReminderStatus | '');
    this.page.set(1);
    this.load();
  }

  onSortChange(value: string): void {
    this.sort.set(value as SortOption);
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  /**
   * Called after a successful create. Switches to a view where the new reminder is guaranteed to be on
   * screen (newest first, page 1, not filtered to Done) and highlights it.
   */
  showCreated(reminder: Reminder): void {
    if (this.status() === 'Done') {
      this.status.set('');
    }
    this.sort.set('createdAt:desc');
    this.page.set(1);
    this.highlightId.set(reminder.id);
    this.load();
  }

  complete(reminder: Reminder): void {
    this.completingId.set(reminder.id);
    this.api.complete(reminder.id).subscribe({
      next: (res) => {
        this.completingId.set(null);
        this.highlightId.set(res.nextOccurrence?.id ?? null);
        this.load();
      },
      error: (err) => {
        this.completingId.set(null);
        this.error.set(describeError(err).message);
        this.load();
      },
    });
  }
}
