import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const injector = inject(Injector);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status !== 401) {
        return throwError(() => error);
      }

      if (
        req.url.includes('/auth/refresh') ||
        req.url.includes('/auth/login') ||
        req.url.includes('/auth/revoke')
      ) {
        return throwError(() => error);
      }

      const auth = injector.get(AuthService);
      return auth.refreshToken().pipe(
        switchMap((res) => {
          if (!res.success || !res.data) {
            auth.logout();
            return throwError(() => error);
          }

          const token = auth.getToken();
          const cloned = req.clone(
            token
              ? {
                  setHeaders: { Authorization: `Bearer ${token}` },
                }
              : {},
          );
          return next(cloned);
        }),
        catchError(() => {
          injector.get(AuthService).logout();
          return throwError(() => error);
        }),
      );
    }),
  );
};
