import { create } from "zustand";

interface UiState {
  /** Whether the mobile navigation drawer is open. */
  isMobileNavOpen: boolean;
  openMobileNav: () => void;
  closeMobileNav: () => void;
  toggleMobileNav: () => void;
}

/**
 * Convention example for client-only UI state (drawers, wizards, filters).
 * Server data belongs to TanStack Query — never store API responses here.
 * New domains get their own file (e.g. stores/filter-store.ts), no mega-store.
 */
export const useUiStore = create<UiState>()((set) => ({
  isMobileNavOpen: false,
  openMobileNav: () => set({ isMobileNavOpen: true }),
  closeMobileNav: () => set({ isMobileNavOpen: false }),
  toggleMobileNav: () => set((state) => ({ isMobileNavOpen: !state.isMobileNavOpen })),
}));
