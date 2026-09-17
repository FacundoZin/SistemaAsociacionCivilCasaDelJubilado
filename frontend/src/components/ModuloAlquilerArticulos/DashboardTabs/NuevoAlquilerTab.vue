<script setup>
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import AlquilerService from '../../../services/AlquilerService'
import SociosService from '../../../services/SociosService'
import ArticuloService from '../../../services/ArticuloService'
import SocioRentStatusCard from '../Alquileres/SocioRentStatusCard.vue'

const router = useRouter()
const emit = defineEmits(['show-toast', 'alquiler-created'])

// State - Nuevo Alquiler
const searchSocioDni = ref('')
const searchingSocio = ref(false)
const foundSocio = ref(null)
const rentCheckStatus = ref(null)
const newAlquilerItems = ref([]) // { articuloId, cantidad, nombre, precio }
const observaciones = ref('')
const isRegisteringAlquiler = ref(false)
const searchError = ref('')
const searchResults = ref([])
const showResults = ref(false)

// State - Articulos available for selection
const articulos = ref([])
const loadingArticulos = ref(false)

// Methods - Articulos
const loadArticulos = async () => {
  loadingArticulos.value = true
  try {
    const result = await ArticuloService.getAll()
    articulos.value = result
  } catch (e) {
    emit('show-toast', { message: 'Error cargando artículos: ' + e.message, type: 'error' })
  } finally {
    loadingArticulos.value = false
  }
}

// Methods - Search Socio
const handleSearchSocio = async () => {
  const queryClean = searchSocioDni.value.replace(/\s+/g, ' ').trim()
  if (!queryClean) return
  searchingSocio.value = true
  foundSocio.value = null
  rentCheckStatus.value = null
  searchError.value = ''
  searchResults.value = []
  showResults.value = false
  try {
    const searchData = await SociosService.search(queryClean, 1, 20)
    let items = []
    if (Array.isArray(searchData)) items = searchData
    else if (searchData && Array.isArray(searchData.items)) items = searchData.items
    else if (searchData && searchData.id) items = [searchData]

    if (items.length === 0) {
      searchError.value = 'No se encontraron socios'
    } else if (items.length === 1) {
      const socio = items[0]
      rentCheckStatus.value = await AlquilerService.getAlquilerStatusBySocio(socio.dni)
    } else {
      searchResults.value = items
      showResults.value = true
    }
  } catch (e) {
    searchError.value = e.message
  } finally {
    searchingSocio.value = false
  }
}

const selectSocioForAlquiler = async (socio) => {
  searchingSocio.value = true
  searchError.value = ''
  try {
    rentCheckStatus.value = await AlquilerService.getAlquilerStatusBySocio(socio.dni)
    searchResults.value = []
    showResults.value = false
  } catch (e) {
    searchError.value = e.message
  } finally {
    searchingSocio.value = false
  }
}

const startNewAlquiler = async () => {
  const status = rentCheckStatus.value
  const idSocio = status?.idSocio ?? status?.IdSocio ?? null
  searchingSocio.value = true
  try {
    let socio = null
    if (idSocio) {
      socio = await SociosService.getById(idSocio)
    } else {
      // fallback: use DNI from status or input
      let dni = status?.dni ?? status?.socioDni ?? status?.dniSocio ?? searchSocioDni.value.replace(/\s+/g, ' ').trim()
      if (!dni && searchResults.value.length > 0) dni = searchResults.value[0].dni
      const dniClean = String(dni || '').replace(/\s/g, '').trim()
      if (!dniClean) throw new Error('No se pudo determinar el socio')
      socio = await SociosService.getByDni(dniClean)
    }
    foundSocio.value = socio
    rentCheckStatus.value = null
    searchResults.value = []
    showResults.value = false
  } catch (e) {
    searchError.value = 'Error al cargar datos del socio: ' + e.message
  } finally {
    searchingSocio.value = false
  }
}

const navigateToAlquiler = (idAlquiler) => {
  router.push(`/ortopedia/alquileres/${idAlquiler}`)
}

const cancelSearch = () => {
  rentCheckStatus.value = null
  searchSocioDni.value = ''
  searchResults.value = []
  showResults.value = false
}

const resetNuevoAlquiler = () => {
  foundSocio.value = null
  rentCheckStatus.value = null
  searchSocioDni.value = ''
  newAlquilerItems.value = []
  observaciones.value = ''
  searchError.value = ''
  searchResults.value = []
  showResults.value = false
  loadArticulos() // Refresh articles availability/prices if needed
}

// Methods - Cart Management
const addArticuloToAlquiler = (articulo) => {
  const existing = newAlquilerItems.value.find((i) => i.articuloId === articulo.id)
  if (existing) {
    existing.cantidad++
  } else {
    newAlquilerItems.value.push({
      articuloId: articulo.id,
      cantidad: 1,
      nombre: articulo.nombre,
      precio: articulo.precioAlquiler,
    })
  }
}

const removeArticuloFromAlquiler = (index) => {
  newAlquilerItems.value.splice(index, 1)
}

const handleRegisterAlquiler = async () => {
  if (newAlquilerItems.value.length === 0) {
    emit('show-toast', { message: 'Debe agregar al menos un artículo', type: 'error' })
    return
  }

  isRegisteringAlquiler.value = true
  try {
    const dto = {
      idSocio: foundSocio.value.id,
      observaciones: observaciones.value,
      items: newAlquilerItems.value.map((i) => ({
        articuloId: i.articuloId,
        cantidad: i.cantidad,
      })),
    }
    const result = await AlquilerService.create(dto)
    emit('show-toast', { message: 'Alquiler registrado correctamente', type: 'success' })
    emit('alquiler-created', result) // Let parent handle redirection to list or detail
    resetNuevoAlquiler()
  } catch (e) {
    emit('show-toast', { message: e.message, type: 'error' })
  } finally {
    isRegisteringAlquiler.value = false
  }
}

// Watchers
watch(searchSocioDni, () => {
  searchError.value = ''
})

onMounted(() => {
  loadArticulos()
})
</script>

<template>
  <div class="h-full">
    <!-- Paso 1: Buscar Socio -->
    <div v-if="!foundSocio && !rentCheckStatus" class="max-w-2xl mx-auto mt-10">
      <div class="bg-white p-10 rounded-3xl border border-slate-200 shadow-xl text-center">
        <div
          class="w-20 h-20 bg-blue-50 text-blue-600 rounded-2xl flex items-center justify-center mx-auto mb-6 shadow-sm ring-1 ring-blue-100/50"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            class="h-10 w-10"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
            />
          </svg>
        </div>
        <h3 class="text-2xl font-bold text-slate-900 mb-3 tracking-tight">Buscar Socio</h3>
        <p class="text-slate-500 mb-10 text-lg">
          Ingrese el DNI, nombre o apellido del socio para iniciar el registro de un nuevo alquiler.
        </p>

        <div class="flex flex-col sm:flex-row gap-3 max-w-lg mx-auto">
          <div class="relative flex-1 group">
            <div class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4">
              <svg
                class="h-5 w-5 text-slate-400 group-focus-within:text-blue-500 transition-colors"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  stroke-width="2"
                  d="M10 6H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V8a2 2 0 00-2-2h-5m-4 0V5a2 2 0 114 0v1m-4 0a2 2 0 104 0m-5 8a2 2 0 100-4 2 2 0 000 4zm5 3h1a2 2 0 012 2v1H2v-1a2 2 0 012-2h1"
                />
              </svg>
            </div>
            <input
              type="text"
              v-model="searchSocioDni"
              @keyup.enter="handleSearchSocio"
              class="block w-full rounded-xl border-slate-300 pl-11 pr-10 focus:border-blue-500 focus:ring-2 focus:ring-blue-500 sm:text-sm py-4 border transition-all shadow-sm"
              placeholder="DNI, nombre o apellido..."
            />
            <div
              v-if="searchingSocio"
              class="absolute inset-y-0 right-0 pr-4 flex items-center pointer-events-none"
            >
              <svg
                class="animate-spin h-5 w-5 text-blue-500"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  class="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  stroke-width="4"
                ></circle>
                <path
                  class="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                ></path>
              </svg>
            </div>
          </div>
          <button
            @click="handleSearchSocio"
            :disabled="searchingSocio || !searchSocioDni"
            class="px-8 py-4 bg-blue-600 text-white rounded-xl font-bold hover:bg-blue-700 disabled:opacity-50 transition-all shadow-lg transform active:scale-95"
          >
            Buscar
          </button>
        </div>

        <div
          v-if="searchError"
          class="mt-6 text-sm font-bold text-red-800 bg-red-50 p-4 rounded-xl border border-red-100 flex items-center justify-center gap-2"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            class="h-5 w-5 text-red-400"
            viewBox="0 0 20 20"
            fill="currentColor"
          >
            <path
              fill-rule="evenodd"
              d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z"
              clip-rule="evenodd"
            />
          </svg>
          {{ searchError }}
        </div>

        <div v-if="showResults && searchResults.length > 0" class="mt-6 text-left">
          <p class="text-sm text-slate-500 mb-3 font-medium">Se encontraron {{ searchResults.length }} socios — seleccione uno:</p>
          <div class="space-y-2 max-h-72 overflow-y-auto pr-1">
            <button v-for="s in searchResults" :key="s.id" @click="selectSocioForAlquiler(s)"
              class="w-full text-left p-4 bg-white border border-slate-200 rounded-xl hover:bg-blue-50 hover:border-blue-200 transition-all flex justify-between items-center group">
              <div>
                <p class="text-sm font-bold text-slate-900 group-hover:text-blue-700">{{ s.apellido }}, {{ s.nombre }}</p>
                <p class="text-xs text-slate-500">DNI: {{ s.dni }}<span v-if="s.localidad"> — {{ s.localidad }}</span></p>
              </div>
              <svg class="w-5 h-5 text-slate-300 group-hover:text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Paso 1.5: Resultado de Verificación (Estado del Socio) -->
    <div v-else-if="rentCheckStatus" class="max-w-2xl mx-auto py-8">
      <SocioRentStatusCard
        :status-data="rentCheckStatus"
        @create-new="startNewAlquiler"
        @view-detail="navigateToAlquiler"
        @cancel="cancelSearch"
      />
    </div>

    <!-- Paso 2: Formulario de Alquiler -->
    <div v-else-if="foundSocio" class="grid grid-cols-1 lg:grid-cols-3 gap-8">
      <!-- Columna Izquierda: Información del Socio y Selección de Artículos -->
      <div class="lg:col-span-2 space-y-8">
        <!-- Socio Info -->
        <div class="bg-white p-6 rounded-3xl border border-slate-200 shadow-sm">
          <div class="flex justify-between items-center mb-6">
            <h3 class="text-sm font-black text-slate-400 uppercase tracking-widest">
              Socio Seleccionado
            </h3>
            <button
              @click="resetNuevoAlquiler"
              class="text-xs font-bold text-blue-600 hover:text-red-500 transition-colors bg-blue-50 px-3 py-1.5 rounded-lg"
            >
              Cambiar Socio
            </button>
          </div>
          <div
            class="flex flex-col sm:items-center sm:flex-row gap-6 bg-slate-50/50 p-6 rounded-2xl border border-slate-100 backdrop-blur-sm"
          >
            <div
              class="w-16 h-16 sm:w-20 sm:h-20 bg-blue-100 text-blue-700 rounded-2xl flex items-center justify-center text-xl sm:text-2xl font-black shadow-sm ring-1 ring-blue-200/50 self-center sm:self-auto"
            >
              {{ foundSocio.nombre[0] }}{{ foundSocio.apellido[0] }}
            </div>
            <div class="flex-1 grid grid-cols-1 sm:grid-cols-3 gap-6 text-center sm:text-left">
              <div class="sm:col-span-1">
                <p class="text-[10px] font-black text-slate-400 uppercase tracking-widest mb-1">
                  Nombre Completo
                </p>
                <p class="text-lg font-bold text-slate-900 leading-tight">
                  {{ foundSocio.apellido }}, {{ foundSocio.nombre }}
                </p>
              </div>
              <div>
                <p class="text-[10px] font-black text-slate-400 uppercase tracking-widest mb-1">
                  Documento (DNI)
                </p>
                <p class="text-lg font-bold text-slate-700">{{ foundSocio.dni }}</p>
              </div>
              <div>
                <p class="text-[10px] font-black text-slate-400 uppercase tracking-widest mb-1">
                  Información de Contacto
                </p>
                <p class="text-lg font-bold text-slate-700">
                  {{ foundSocio.telefono || 'Sin teléfono' }}
                </p>
              </div>
            </div>
          </div>
        </div>

        <!-- Artículos Disponibles -->
        <div class="bg-white p-6 rounded-3xl border border-slate-200 shadow-sm">
          <div class="flex justify-between items-center mb-6">
            <h3 class="text-sm font-black text-slate-400 uppercase tracking-widest">
              Artículos Disponibles
            </h3>
            <span class="text-[10px] font-bold text-slate-400 bg-slate-100 px-2 py-1 rounded-full"
              >{{ articulos.length }} en inventario</span
            >
          </div>

          <div v-if="loadingArticulos" class="flex justify-center py-12">
            <svg
              class="animate-spin h-8 w-8 text-blue-600"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                class="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                stroke-width="4"
              ></circle>
              <path
                class="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              ></path>
            </svg>
          </div>

          <div v-else-if="articulos.length > 0" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div
              v-for="art in articulos"
              :key="art.id"
              class="border border-slate-100 rounded-2xl p-4 flex justify-between items-center hover:bg-blue-50/30 hover:border-blue-100 transition-all group cursor-default"
            >
              <div>
                <p class="font-bold text-slate-800 group-hover:text-blue-700 transition-colors">
                  {{ art.nombre }}
                </p>
                <p class="text-sm text-blue-600 font-black mt-1">
                  ${{ art.precioAlquiler.toLocaleString() }}
                  <span class="text-[10px] font-bold text-slate-400 uppercase tracking-tight ml-1"
                    >/ mes</span
                  >
                </p>
              </div>
              <button
                @click="addArticuloToAlquiler(art)"
                class="p-3 bg-blue-50 text-blue-600 rounded-xl sm:opacity-0 sm:group-hover:opacity-100 hover:bg-blue-600 hover:text-white transition-all transform group-active:scale-90"
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  class="h-5 w-5"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M12 4v16m8-8H4"
                  />
                </svg>
              </button>
            </div>
          </div>
          <div
            v-else
            class="text-center py-12 bg-slate-50 rounded-2xl border border-dashed border-slate-200"
          >
            <p class="text-sm font-bold text-slate-400">No hay artículos cargados en el sistema.</p>
          </div>
        </div>
      </div>

      <!-- Columna Derecha: Resumen y Confirmación -->
      <div class="space-y-6">
        <div
          class="bg-white p-8 rounded-3xl border border-slate-200 shadow-xl sticky top-6 overflow-hidden"
        >
          <div
            class="absolute top-0 right-0 w-32 h-32 bg-blue-50/50 rounded-full -mr-16 -mt-16 -z-10"
          ></div>

          <h3 class="text-lg font-black text-slate-900 mb-6 tracking-tight">
            Resumen del Alquiler
          </h3>

          <!-- Items List -->
          <div class="space-y-4 mb-8">
            <div
              v-if="newAlquilerItems.length === 0"
              class="flex flex-col items-center justify-center py-12 px-4 bg-slate-50 border-2 border-dashed border-slate-200 rounded-3xl text-slate-400 group"
            >
              <div
                class="p-4 bg-white rounded-full shadow-sm mb-4 group-hover:scale-110 transition-transform"
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  class="h-8 w-8 text-slate-300"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    stroke-width="2"
                    d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z"
                  />
                </svg>
              </div>
              <p class="text-xs font-bold uppercase tracking-widest text-center">Sin artículos</p>
              <p class="text-[10px] text-slate-400 mt-1 text-center font-medium italic">
                Agregue productos de la lista
              </p>
            </div>
            <ul v-else class="divide-y divide-slate-100">
              <li
                v-for="(item, index) in newAlquilerItems"
                :key="index"
                class="py-4 flex justify-between items-start animate-in fade-in slide-in-from-right-2"
              >
                <div>
                  <p class="text-sm font-bold text-slate-900">{{ item.nombre }}</p>
                  <div class="flex items-center gap-2 mt-1">
                    <span
                      class="text-[10px] font-black bg-blue-50 text-blue-600 px-1.5 py-0.5 rounded uppercase tracking-tighter"
                      >Cant: {{ item.cantidad }}</span
                    >
                    <span class="text-[10px] text-slate-400 font-bold tracking-tight"
                      >${{ item.precio.toLocaleString() }} c/u</span
                    >
                  </div>
                </div>
                <div class="flex items-center gap-3">
                  <p class="text-sm font-black text-blue-600">
                    ${{ (item.precio * item.cantidad).toLocaleString() }}
                  </p>
                  <button
                    @click="removeArticuloFromAlquiler(index)"
                    class="p-1.5 bg-red-50 text-red-500 rounded-lg hover:bg-red-500 hover:text-white transition-all transform active:scale-90"
                  >
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      class="h-4 w-4"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"
                      />
                    </svg>
                  </button>
                </div>
              </li>
            </ul>
          </div>

          <!-- Total -->
          <div
            v-if="newAlquilerItems.length > 0"
            class="flex flex-col gap-1 py-6 border-t border-slate-100"
          >
            <span
              class="text-[10px] font-black text-slate-400 uppercase tracking-widest text-center"
              >Inversión mensual</span
            >
            <span class="text-3xl font-black text-slate-900 text-center tracking-tight">
              ${{
                newAlquilerItems
                  .reduce((sum, item) => sum + item.precio * item.cantidad, 0)
                  .toLocaleString()
              }}
            </span>
          </div>

          <!-- Observaciones -->
          <div class="mb-8">
            <label
              for="observaciones"
              class="block text-[10px] font-black text-slate-400 uppercase tracking-widest mb-3"
            >
              Observaciones del Alquiler
            </label>
            <textarea
              id="observaciones"
              v-model="observaciones"
              rows="3"
              class="focus:ring-2 focus:ring-blue-500 focus:border-blue-500 block w-full sm:text-sm border-slate-200 rounded-2xl p-4 bg-slate-50/50 transition-all placeholder:text-slate-400"
              placeholder="¿Algún detalle especial o recordatorio?..."
            ></textarea>
          </div>

          <!-- Button -->
          <button
            @click="handleRegisterAlquiler"
            :disabled="isRegisteringAlquiler || newAlquilerItems.length === 0"
            class="w-full flex justify-center py-4 px-6 border border-transparent rounded-2xl shadow-lg shadow-blue-200 text-sm font-black uppercase tracking-widest text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed transition-all transform active:scale-95"
          >
            <svg
              v-if="isRegisteringAlquiler"
              class="animate-spin -ml-1 mr-3 h-5 w-5 text-white"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                class="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                stroke-width="4"
              ></circle>
              <path
                class="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              ></path>
            </svg>
            <span v-if="!isRegisteringAlquiler">Confirmar Alquiler</span>
            <span v-else>Procesando...</span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
