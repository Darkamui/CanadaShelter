import { Building2, DoorOpen, Fence, House, MapPin } from 'lucide-react';

const icons: Record<string, typeof MapPin> = {
  building: Building2,
  room: DoorOpen,
  kennel: Fence,
  foster_home: House,
};

/** The icon of a location kind; kinds the organization added get a pin. */
export function LocationKindIcon({ code }: { code: string }) {
  const Icon = icons[code] ?? MapPin;
  return <Icon aria-hidden="true" />;
}
