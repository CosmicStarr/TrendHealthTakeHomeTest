import { Component } from '@angular/core';
import { ReminderForm } from './reminders/reminder-form';
import { ReminderList } from './reminders/reminder-list';

@Component({
  imports: [ReminderForm, ReminderList],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
