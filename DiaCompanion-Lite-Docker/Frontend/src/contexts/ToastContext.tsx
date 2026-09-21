import {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from "react";

/* Thông báo nhanh (toast). Tự biến mất sau ~4s. */
type ToastKind = "success" | "error" | "info";
interface Toast {
  id: number;
  text: string;
  kind: ToastKind;
}

interface ToastValue {
  push: (text: string, kind?: ToastKind) => void;
}

const ToastContext = createContext<ToastValue | null>(null);
let seq = 1;

export function ToastProvider({ children }: { children?: ReactNode }) {
  const [items, setItems] = useState<Toast[]>([]);

  const push = useCallback((text: string, kind: ToastKind = "info") => {
    const id = seq++;
    setItems((x) => [...x, { id, text, kind }]);
    setTimeout(() => setItems((x) => x.filter((t) => t.id !== id)), 4200);
  }, []);

  return (
    <ToastContext.Provider value={{ push }}>
      {children}
      <div className="toast-stack">
        {items.map((t) => (
          <div key={t.id} className={`toast-item ${t.kind}`}>
            {t.text}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast phải nằm trong <ToastProvider>");
  return ctx;
}
