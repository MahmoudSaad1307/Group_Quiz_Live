"use client";

import type { SanitizedOptionDto } from "@/lib/realtime/events";

const BUTTON_THEMES = [
  { bg: "bg-red-600 hover:bg-red-500", label: "Triangle" },
  { bg: "bg-blue-600 hover:bg-blue-500", label: "Diamond" },
  { bg: "bg-amber-500 hover:bg-amber-400", label: "Circle" },
  { bg: "bg-emerald-600 hover:bg-emerald-500", label: "Square" },
] as const;

function OptionShape({ index }: { index: number }) {
  switch (index % 4) {
    case 0:
      return (
        <svg viewBox="0 0 24 24" className="h-6 w-6 shrink-0 fill-current" aria-hidden="true">
          <polygon points="12,3 22,20 2,20" />
        </svg>
      );
    case 1:
      return (
        <svg viewBox="0 0 24 24" className="h-6 w-6 shrink-0 fill-current" aria-hidden="true">
          <polygon points="12,2 22,12 12,22 2,12" />
        </svg>
      );
    case 2:
      return (
        <svg viewBox="0 0 24 24" className="h-6 w-6 shrink-0 fill-current" aria-hidden="true">
          <circle cx="12" cy="12" r="9" />
        </svg>
      );
    default:
      return (
        <svg viewBox="0 0 24 24" className="h-6 w-6 shrink-0 fill-current" aria-hidden="true">
          <rect x="4" y="4" width="16" height="16" rx="2" />
        </svg>
      );
  }
}

export function AnswerButtons({
  options,
  disabled,
  selectedOptionId,
  onSelect,
}: {
  options: SanitizedOptionDto[];
  disabled: boolean;
  selectedOptionId: string | null;
  onSelect: (optionId: string) => void;
}) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 w-full">
      {options.map((opt, idx) => {
        const theme = BUTTON_THEMES[idx % 4]!;
        const isSelected = selectedOptionId === opt.id;
        return (
          <button
            key={opt.id}
            type="button"
            disabled={disabled}
            onClick={() => onSelect(opt.id)}
            aria-label={`${theme.label}: ${opt.text}`}
            className={`min-h-[64px] rounded-2xl px-5 py-4 text-white font-bold text-lg flex items-center gap-3 shadow-md transition active:scale-[0.98] disabled:opacity-60 ${theme.bg} ${
              isSelected ? "ring-4 ring-white" : ""
            }`}
          >
            <OptionShape index={idx} />
            <span className="text-start break-words min-w-0 flex-1">{opt.text}</span>
          </button>
        );
      })}
    </div>
  );
}
