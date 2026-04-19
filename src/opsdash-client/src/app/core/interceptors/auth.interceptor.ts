import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';

const TOKEN_KEY = 'opsdash_token';

function isOpsDashApiRequest(url: string): boolean {
  const api = environment.apiUrl.replace(/\/$/, '');
  if (!api) {
    return false;
  }

  if (url.includes('/api/v1')) {
    return true;
  }

  try {
    const parsed = new URL(url, window.location.origin);
    const path = parsed.pathname.replace(/\/$/, '');
    const basePath = api.startsWith('/') ? api : `/${api}`;
    return path.startsWith(basePath) || path.includes('/api/v1');
  } catch {
    return url.includes('/api/v1');
  }
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isOpsDashApiRequest(req.url)) {
    return next(req);
  }

  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) {
    return next(req);
  }

  const cloned = req.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`,
    },
  });

  return next(cloned);
};
