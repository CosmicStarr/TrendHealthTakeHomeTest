export type ReminderStatus = 'Open' | 'Done';
export type Recurrence = 'None' | 'Daily' | 'Weekly';
export type SortField = 'dueDate' | 'createdAt';
export type SortDirection = 'asc' | 'desc';

export interface Reminder {
  id: number;
  patientName: string;
  title: string;
  notes: string;
  dueDate: string;
  status: ReminderStatus;
  recurrence: Recurrence;
  isRecurring: boolean;
  createdAt: string;
  completedAt: string | null;
  sourceReminderId: number | null;
}

export interface CreateReminderRequest {
  patientName: string;
  title: string;
  notes: string;
  /** ISO 8601 with an offset, e.g. 2026-11-05T14:00:00.000Z */
  dueDate: string;
  recurrence: Recurrence;
}

export interface ReminderQuery {
  page: number;
  pageSize: number;
  status?: ReminderStatus;
  sortBy: SortField;
  sortDir: SortDirection;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface CompleteReminderResponse {
  completed: Reminder;
  nextOccurrence: Reminder | null;
}

/** Error body the API returns for 4xx/5xx responses. `errors` is only present on 400s. */
export interface ApiError {
  statusCode: number;
  message: string;
  errors?: Record<string, string[]>;
}
