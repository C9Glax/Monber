export interface LastLocation {
  lat: number
  lon: number
  placeLabel: string
  radiusKm: number
}

const COOKIE_NAME = 'monber-last-location'
const COOKIE_MAX_AGE = 60 * 60 * 24 * 365

export function useLastLocation() {
  const cookie = useCookie<LastLocation | null>(COOKIE_NAME, {
    maxAge: COOKIE_MAX_AGE,
    sameSite: 'lax',
    default: () => null,
  })

  function loadLastLocation(): LastLocation | null {
    return cookie.value
  }

  function saveLastLocation(location: LastLocation) {
    cookie.value = location
  }

  return { loadLastLocation, saveLastLocation }
}
