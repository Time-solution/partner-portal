export type Lang = "ar" | "en";

type Dict = Record<string, string>;

export const translations: Record<Lang, Dict> = {
  ar: {
    brand: "منصة شركاء زاهي",
    signInTitle: "تسجيل الدخول",
    signInSubtitle: "ادخل إلى لوحة تحكم الشريك الخاصة بك",
    email: "البريد الإلكتروني أو اسم المستخدم",
    emailPlaceholder: "name@example.com",
    password: "كلمة المرور",
    forgotPassword: "نسيت كلمة المرور؟",
    rememberMe: "تذكرني",
    signIn: "تسجيل الدخول",
    signingIn: "جارٍ تسجيل الدخول…",
    errorRequired: "يرجى إدخال البريد الإلكتروني وكلمة المرور.",
    errorInvalid: "بيانات الاعتماد غير صحيحة. حاول مرة أخرى.",
    secureNotice: "اتصال آمن عبر OpenIddict (OIDC).",
    toggleToEnglish: "English",
    toggleTheme: "تبديل السمة",
    showPassword: "إظهار كلمة المرور",
    hidePassword: "إخفاء كلمة المرور",
  },
  en: {
    brand: "Zahy Partner Platform",
    signInTitle: "Sign in",
    signInSubtitle: "Access your partner dashboard",
    email: "Email or username",
    emailPlaceholder: "name@example.com",
    password: "Password",
    forgotPassword: "Forgot password?",
    rememberMe: "Remember me",
    signIn: "Sign in",
    signingIn: "Signing in…",
    errorRequired: "Please enter your email and password.",
    errorInvalid: "Invalid credentials. Please try again.",
    secureNotice: "Secured by OpenIddict (OIDC).",
    toggleToEnglish: "العربية",
    toggleTheme: "Toggle theme",
    showPassword: "Show password",
    hidePassword: "Hide password",
  },
};

export function useTranslator(lang: Lang) {
  return (key: keyof (typeof translations)["en"]) => translations[lang][key] ?? key;
}
