const API_URL = `${import.meta.env.VITE_API_URL}/Socios`

const handleError = async (response, defaultMessage) => {
  if (response.status >= 500) {
    throw new Error('Algo salió mal en el servidor. Por favor intente más tarde.')
  }
  const errorText = await response.text()
  if (!errorText) return defaultMessage

  try {
    const errorObj = JSON.parse(errorText)
    // Si es un error de ModelState (.NET)
    if (errorObj.errors) {
      const firstErrorKey = Object.keys(errorObj.errors)[0]
      return errorObj.errors[firstErrorKey][0]
    }
    return errorObj.mensaje || errorObj.message || errorText || defaultMessage
  } catch (e) {
    return errorText || defaultMessage
  }
}

export default {
  async removeSocio(id) {
    const response = await fetch(`${API_URL}/${id}`, {
      method: 'DELETE',
      credentials: 'include',
    })

    if (!response.ok) {
      const msg = await handleError(response, 'Error al dar de baja al socio')
      throw new Error(msg)
    }

    return true
  },

  async getByDni(dni) {
    const clean = String(dni).replace(/\s/g, '').trim()
    if (!clean) throw new Error('Debe indicar un DNI válido')
    const response = await fetch(`${API_URL}/${encodeURIComponent(clean)}`, { credentials: 'include' })

    if (!response.ok) {
      const msg = await handleError(response, 'Error al buscar socio')
      throw new Error(msg)
    }

    return await response.json()
  },

  async search(query, page = 1, pageSize = 10) {
    const clean = String(query ?? '').replace(/\s+/g, ' ').trim()
    if (!clean) throw new Error('Debe indicar un término de búsqueda')
    const params = new URLSearchParams({ q: clean, page: String(page), pageSize: String(pageSize) })
    const response = await fetch(`${API_URL}/search?${params.toString()}`, { credentials: 'include' })

    if (!response.ok) {
      const msg = await handleError(response, 'Error al buscar socios')
      throw new Error(msg)
    }

    return await response.json()
  },

  async getById(id) {
    const response = await fetch(`${API_URL}/byId/${id}`, { credentials: 'include' })
    if (!response.ok) {
      const msg = await handleError(response, 'Error al obtener datos del socio')
      throw new Error(msg)
    }
    return await response.json()
  },

  async getFullDetail(id) {
    const response = await fetch(`${API_URL}/full/${id}`, { credentials: 'include' })
    if (!response.ok) {
      const msg = await handleError(response, 'No se pudo cargar la información del socio')
      throw new Error(msg)
    }
    return await response.json()
  },

  async getDebtors(page = 1, pageSize = 10) {
    const response = await fetch(`${API_URL}/deudores?pageNumber=${page}&pageSize=${pageSize}`, {
      credentials: 'include',
    })
    if (!response.ok) {
      const msg = await handleError(response, 'Error al obtener lista de deudores')
      throw new Error(msg)
    }
    return await response.json()
  },

  async create(dto) {
    const response = await fetch(`${API_URL}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      credentials: 'include',
      body: JSON.stringify(dto),
    })

    if (response.status === 409) {
      const conflictData = await response.json()
      const error = new Error('Socio en conflicto')
      error.status = 409
      error.data = conflictData
      throw error
    }

    if (!response.ok) {
      const msg = await handleError(response, 'Error al crear el socio')
      throw new Error(msg)
    }

    return await response.json()
  },

  async update(id, dto) {
    const response = await fetch(`${API_URL}/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      credentials: 'include',
      body: JSON.stringify(dto),
    })

    if (!response.ok) {
      const msg = await handleError(response, 'Error al actualizar el socio')
      throw new Error(msg)
    }

    return await response.json()
  },

  async reactivar(id) {
    const response = await fetch(`${API_URL}/reactivar/${id}`, {
      method: 'POST',
      credentials: 'include',
    })

    if (!response.ok) {
      const msg = await handleError(response, 'Error al reactivar el socio')
      throw new Error(msg)
    }

    return await response.json()
  },
}
