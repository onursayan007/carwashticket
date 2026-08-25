// Hizmetin görsel kimliği: su mavi, köpük pembe, sünger sarısı.
// Ada göre eşleşiyor çünkü hizmetleri işyerleri kendi adlandırıyor.
export interface ServiceLook {
  icon: string
  chip: string
  ring: string
}

const LOOKS: { match: RegExp; look: ServiceLook }[] = [
  {
    match: /su|durulama|yıkama/i,
    look: {
      icon: '💧',
      chip: 'bg-brand-water-soft text-brand-water',
      ring: 'ring-brand-water',
    },
  },
  {
    match: /köpük|şampuan/i,
    look: {
      icon: '🫧',
      chip: 'bg-brand-foam-soft text-brand-foam',
      ring: 'ring-brand-foam',
    },
  },
  {
    match: /fırça|sünger|süpürge|paspas/i,
    look: {
      icon: '🧽',
      chip: 'bg-brand-sponge-soft text-brand-sponge',
      ring: 'ring-brand-sponge',
    },
  },
  {
    match: /cila|pasta|seramik|detailing/i,
    look: {
      icon: '✨',
      chip: 'bg-brand-blue-soft text-brand-blue',
      ring: 'ring-brand-blue',
    },
  },
]

const FALLBACK: ServiceLook = {
  icon: '🚗',
  chip: 'bg-brand-sky text-brand-navy',
  ring: 'ring-brand-navy',
}

export function serviceLook(name: string): ServiceLook {
  return LOOKS.find((entry) => entry.match.test(name))?.look ?? FALLBACK
}
