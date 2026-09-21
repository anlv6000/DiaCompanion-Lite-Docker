import type { LoginResponse, Role, StaffUserDto } from "@/types/api";

export type RoleSource =
  | Role
  | readonly Role[]
  | Pick<LoginResponse, "role" | "roles">
  | Pick<StaffUserDto, "role" | "roles">
  | null
  | undefined;

export function getRoles(source: RoleSource): Role[] {
  if (!source) return [];
  if (typeof source === "string") return [source];
  if (Array.isArray(source)) return [...new Set(source)];

  const value = source as { role?: Role; roles?: Role[] };
  const all = [...(value.roles || []), ...(value.role ? [value.role] : [])];
  return [...new Set(all)];
}

export function hasRole(source: RoleSource, role: Role): boolean {
  return getRoles(source).includes(role);
}

export function hasAnyRole(source: RoleSource, roles: readonly Role[]): boolean {
  const current = getRoles(source);
  return roles.some((role) => current.includes(role));
}

export function roleLabel(role: Role): string {
  switch (role) {
    case "Doctor": return "Bác sĩ";
    case "Receptionist": return "Lễ tân";
    case "Patient": return "Bệnh nhân";
    case "Research": return "Nghiên cứu viên";
    default: return "Admin";
  }
}

export function rolesLabel(source: RoleSource): string {
  // Đây là web nội bộ bệnh viện nên không hiển thị role Patient.
  const values = getRoles(source).filter(role => role !== "Patient");

  return values.length
    ? values.map(roleLabel).join(", ")
    : "—";
}

/** Role ưu tiên để chọn landing page của WEB console. */
export function primaryWebRole(source: RoleSource): Role | undefined {
  const values = getRoles(source);
  if (values.includes("Admin")) return "Admin";
  if (values.includes("Doctor")) return "Doctor";
  if (values.includes("Receptionist")) return "Receptionist";
  if (values.includes("Patient")) return "Patient";
  return undefined;
}
