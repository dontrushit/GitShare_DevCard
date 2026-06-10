import { AlertCircle } from 'lucide-react';

interface ErrorAlertProps {
  message: string;
}

export function ErrorAlert({ message }: ErrorAlertProps) {
  return (
    <section
      role="alert"
      className="card-panel flex items-start gap-2 border-red-500/30 bg-red-500/10 px-3 py-2"
    >
      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-400" />
      <p className="text-xs leading-relaxed text-red-200">{message}</p>
    </section>
  );
}
