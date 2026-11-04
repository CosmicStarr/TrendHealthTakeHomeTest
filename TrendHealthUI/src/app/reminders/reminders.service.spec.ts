import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RemindersService, describeError } from './reminders.service';

describe('RemindersService', () => {
  let service: RemindersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RemindersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends paging, sorting and status as query parameters', () => {
    service
      .list({ page: 2, pageSize: 10, status: 'Done', sortBy: 'createdAt', sortDir: 'desc' })
      .subscribe();

    const req = http.expectOne((r) => r.url === '/reminders');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    expect(req.request.params.get('status')).toBe('Done');
    expect(req.request.params.get('sortBy')).toBe('createdAt');
    expect(req.request.params.get('sortDir')).toBe('desc');
  });

  it('omits status when no filter is selected', () => {
    service.list({ page: 1, pageSize: 10, sortBy: 'dueDate', sortDir: 'asc' }).subscribe();
    expect(http.expectOne((r) => r.url === '/reminders').request.params.has('status')).toBe(false);
  });
});

describe('describeError', () => {
  it('extracts message and field errors from an API validation response', () => {
    const err = new HttpErrorResponse({
      status: 400,
      error: {
        statusCode: 400,
        message: 'One or more validation errors occurred.',
        errors: { title: ['Required'] },
      },
    });
    expect(describeError(err)).toEqual({
      message: 'One or more validation errors occurred.',
      fieldErrors: { title: ['Required'] },
    });
  });

  it('explains a network failure', () => {
    expect(describeError(new HttpErrorResponse({ status: 0 })).message).toContain(
      'Could not reach the API',
    );
  });
});
