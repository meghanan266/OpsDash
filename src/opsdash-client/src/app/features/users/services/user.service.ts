import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import type { ApiResponse, PagedResult } from '../../../core/models/common.model';
import type { User } from '../../../core/models/user.model';

export interface RoleDto {
  id: number;
  name: string;
}

export interface GetUsersParams {
  searchTerm?: string;
  roleId?: number;
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: string;
}

export interface CreateUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  roleId: number;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  roleId?: number;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly api = inject(ApiService);

  getUsers(params?: GetUsersParams): Observable<ApiResponse<PagedResult<User>>> {
    return this.api.get<PagedResult<User>>('/users', {
      searchTerm: params?.searchTerm,
      roleId: params?.roleId,
      isActive: params?.isActive,
      page: params?.page,
      pageSize: params?.pageSize,
      sortBy: params?.sortBy,
      sortDirection: params?.sortDirection,
    });
  }

  getUserById(id: number): Observable<ApiResponse<User>> {
    return this.api.get<User>(`/users/${id}`);
  }

  createUser(request: CreateUserRequest): Observable<ApiResponse<User>> {
    return this.api.post<User>('/users', request);
  }

  updateUser(id: number, request: UpdateUserRequest): Observable<ApiResponse<User>> {
    return this.api.put<User>(`/users/${id}`, request);
  }

  deleteUser(id: number): Observable<ApiResponse<boolean>> {
    return this.api.delete<boolean>(`/users/${id}`);
  }

  searchUsers(query: string): Observable<ApiResponse<User[]>> {
    return this.api.get<User[]>('/users/search', { q: query });
  }

  getRoles(): Observable<ApiResponse<RoleDto[]>> {
    return this.api.get<RoleDto[]>('/users/roles');
  }
}
