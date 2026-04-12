import {Injectable, signal} from '@angular/core';
import {HttpClient, HttpHeaders} from '@angular/common/http';
import {inject } from '@angular/core';
import {Observable, map, catchError, of} from 'rxjs';
@Injectable({providedIn: 'root'})
export class AuthService {
  private http = inject(HttpClient);

  user = signal<{name: string, email: string} | null>(null);
  checkAuth(): Observable<boolean> {
    return this.http.get<{name: string, email: string}>('api/auth/me').pipe(
      map(user => {
        this.user.set(user);
        return true;
      }),
      catchError(() => {
        this.user.set(null);
        return of(false);
      })
    )
  }
}
