import {
  useCallback,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { createPortal } from 'react-dom';

interface HoverPortalPopoverProps {
  id?: string;
  width?: number;
  estimatedHeight?: number;
  className?: string;
  children: ReactNode;
  trigger: ReactNode;
}

export function HoverPortalPopover({
  id,
  width = 208,
  estimatedHeight = 280,
  className = '',
  children,
  trigger,
}: HoverPortalPopoverProps) {
  const triggerRef = useRef<HTMLSpanElement>(null);
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState({ top: 0, left: 0 });

  const updatePosition = useCallback(() => {
    const el = triggerRef.current;
    if (!el) {
      return;
    }

    const rect = el.getBoundingClientRect();
    const margin = 8;
    let top = rect.bottom + margin;
    let left = rect.left;

    if (top + estimatedHeight > window.innerHeight - margin) {
      top = Math.max(margin, rect.top - estimatedHeight - margin);
    }

    left = Math.min(Math.max(margin, left), window.innerWidth - width - margin);
    setPosition({ top, left });
  }, [estimatedHeight, width]);

  useLayoutEffect(() => {
    if (!open) {
      return;
    }

    updatePosition();
    window.addEventListener('scroll', updatePosition, true);
    window.addEventListener('resize', updatePosition);
    return () => {
      window.removeEventListener('scroll', updatePosition, true);
      window.removeEventListener('resize', updatePosition);
    };
  }, [open, updatePosition]);

  const show = () => {
    updatePosition();
    setOpen(true);
  };

  const hide = () => setOpen(false);

  const popover =
    open &&
    createPortal(
      <div
        id={id}
        role="tooltip"
        style={{ top: position.top, left: position.left, width }}
        className={`pointer-events-none fixed z-[200] flex flex-col rounded-lg border border-zinc-700 bg-zinc-950 py-1.5 shadow-2xl ring-1 ring-zinc-800 ${className}`.trim()}
      >
        {children}
      </div>,
      document.body,
    );

  return (
    <>
      <span
        ref={triggerRef}
        tabIndex={0}
        className="inline-flex cursor-default outline-none focus-visible:ring-2 focus-visible:ring-zinc-500 focus-visible:ring-offset-2 focus-visible:ring-offset-zinc-950"
        aria-describedby={open ? id : undefined}
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocus={show}
        onBlur={hide}
      >
        {trigger}
      </span>
      {popover}
    </>
  );
}
