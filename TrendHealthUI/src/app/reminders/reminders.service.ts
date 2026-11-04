import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiError,
  CompleteReminderResponse,
  CreateReminderRequest,
  PagedResponse,
  Reminder,
  ReminderQuery,
} from './reminder.models';

/** Relative URL: in development, `ng serve` forwards /reminders to the API (see proxy.conf.json). */
const BASE_URL = '/reminders';

@Injectable({ providedIn: 'root' })
export class RemindersService {
  private readonly http = inject(HttpClient);

  list(query: ReminderQuery): Observable<PagedResponse<Reminder>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDir', query.sortDir);
    if (query.status) {
      params = params.set('status', query.status);
    }
    return this.http.get<PagedResponse<Reminder>>(BASE_URL, { params });
  }

  create(request: CreateReminderRequest): Observable<Reminder> {
    return this.http.post<Reminder>(BASE_URL, request);
  }

  complete(id: number): Observable<CompleteReminderResponse> {
    return this.http.post<CompleteReminderResponse>(`${BASE_URL}/${id}/complete`, null);
  }
}

export interface DescribedError {
  message: string;
  fieldErrors: Record<string, string[]>;
}

/** Converts an HttpClient error into a message to show the user, plus any per-field validation errors. */
export function describeError(err: unknown): DescribedError {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) {
      return { message: 'Could not reach the API. Is it running?', fieldErrors: {} };
    }
    const body = err.error as Partial<ApiError> | null;
    return {
      message: body?.message ?? `Request failed (${err.status}).`,
      fieldErrors: body?.errors ?? {},
    };
  }
  return { message: 'Something went wrong.', fieldErrors: {} };
}
