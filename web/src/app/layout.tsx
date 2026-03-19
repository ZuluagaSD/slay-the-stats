import type { Metadata } from "next";
import { Cinzel, Geist_Mono } from "next/font/google";
import "./globals.css";

const cinzel = Cinzel({ subsets: ["latin"], variable: "--font-cinzel", weight: ["400", "700", "900"] });
const geistMono = Geist_Mono({ subsets: ["latin"], variable: "--font-geist-mono" });

export const metadata: Metadata = {
  title: "Slay the Stats",
  description: "Combat analytics for Slay the Spire 2",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`dark ${cinzel.variable} ${geistMono.variable}`}>
      <body className="antialiased flex min-h-screen text-slate-200 bg-[#120F16]">
        <main className="flex-1 w-full min-h-screen relative overflow-x-hidden">
          {/* Subtle runic background overlay */}
          <div className="absolute inset-0 pointer-events-none opacity-20 bg-[radial-gradient(ellipse_at_center,_var(--tw-gradient-stops))] from-purple-900/40 via-[#120F16]/10 to-[#120F16]"></div>
          <div className="relative z-10 w-full h-full p-4 lg:p-8 max-w-[1600px] mx-auto flex flex-col items-center">
            {children}
          </div>
        </main>
      </body>
    </html>
  );
}
