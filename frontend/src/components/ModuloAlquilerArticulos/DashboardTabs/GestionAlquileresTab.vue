<script setup>
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import AlquilerService from '../../../services/AlquilerService'
import SociosService from '../../../services/SociosService'
import AlquilerCard from '../Alquileres/AlquilerCard.vue'
import Pagination from '../../Common/Pagination.vue'

const router = useRouter()
const emit = defineEmits(['show-toast', 'view-detail'])

// State
const alquileres = ref([])
const loadingAlquileres = ref(false)
const searchQuery = ref('')
const searchDni = searchQuery // compat alias
const searchResultAlquileres = ref([])
const isSearching = ref(false)
const searchError = ref('')
const sociosMatches = ref([])
const showSocioSelector = ref(false)

// Pagination
const currentPage = ref(1)
const pageSize = ref(12)
const totalCount = ref(0)
const totalPages = ref(0)

// Methods
const loadAlquileresActivos = async () => {
  loadingAlquileres.value = true
  searchResultAlquileres.value = [] // Reset search
  isSearching.value = false
  searchQuery.value = ''
  sociosMatches.value = []
  showSocioSelector.value = false
  try {
    const result = await AlquilerService.getAllActive(currentPage.value, pageSize.value)
    alquileres.value = result.items
    totalCount.value = result.totalCount
    totalPages.value = result.totalPages
  } catch (e) {
    emit('show-toast', {
      message: 'Error cargando alquileres activos: ' + e.message,
      type: 'error',
    })
  } finally {
    loadingAlquileres.value = false
  }
}

const handlePageChange = (newPage) => {
  currentPage.value = newPage
  loadAlquileresActivos()
}

const fetchAlquileresByDni = async (dni) => {
  loadingAlquileres.value = true
  isSearching.value = true
  searchError.value = ''
  try {
    const result = await AlquilerService.getBySocio(dni)
    searchResultAlquileres.value = result
    if (result.length === 0) searchError.value = 'No se encontraron alquileres para el socio seleccionado'
  } catch (e) {
    searchError.value = e.message
    searchResultAlquileres.value = []
  } finally {
    loadingAlquileres.value = false
  }
}

const selectSocioForAlquileres = (socio) => {
  showSocioSelector.value = false
  sociosMatches.value = []
  searchQuery.value = socio.dni
  fetchAlquileresByDni(socio.dni)
}

const handleSearch = async () => {
  const queryClean = searchQuery.value.replace(/\s+/g, ' ').trim()
  if (!queryClean) {
    loadAlquileresActivos()
    return
  }

  // Heuristic: if query looks like DNI (only digits), try direct alquiler fetch first
  const isDniLike = /^\d+$/.test(queryClean.replace(/\s/g, ''))

  loadingAlquileres.value = true
  isSearching.value = true
  searchError.value = ''
  sociosMatches.value = []
  showSocioSelector.value = false
  try {
    if (isDniLike) {
      try {
        const result = await AlquilerService.getBySocio(queryClean)
        if (result.length > 0) {
          searchResultAlquileres.value = result
          return
        }
        // If no alquileres but DNI valid, fall through to socio search to verify existence
      } catch (e) {
        // fall through to socio search
      }
    }

    // Search socios by query (DNI, nombre, apellido)
    const sociosData = await SociosService.search(queryClean, 1, 20)
    let items = []
    if (Array.isArray(sociosData)) items = sociosData
    else if (sociosData && Array.isArray(sociosData.items)) items = sociosData.items
    else if (sociosData && sociosData.id) items = [sociosData]

    if (items.length === 0) {
      searchError.value = 'No se encontraron socios con ese criterio'
      searchResultAlquileres.value = []
    } else if (items.length === 1) {
      await fetchAlquileresByDni(items[0].dni)
    } else {
      sociosMatches.value = items
      showSocioSelector.value = true
      searchResultAlquileres.value = []
    }
  } catch (e) {
    searchError.value = e.message
    searchResultAlquileres.value = []
  } finally {
    loadingAlquileres.value = false
  }
}

const goToAlquilerDetail = (id) => {
  // Can emit to parent to handle navigation, or just navigate here.
  // Parent method was: router.push(`/ortopedia/alquileres/${id}`)
  router.push(`/ortopedia/alquileres/${id}`)
}

// Watchers
watch(searchQuery, () => {
  searchError.value = ''
})

onMounted(() => {
  loadAlquileresActivos()
})
</script>

<template>
  <div>
    <!-- Actions / Search Bar -->
    <div
      class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 mb-8 bg-slate-50 p-5 rounded-2xl border border-slate-200 shadow-sm transition-all"
    >
      <div class="w-full sm:w-auto flex-1 max-w-lg">
        <label
          for="search-query"
          class="block text-xs font-black text-slate-400 uppercase tracking-widest mb-2"
          >Buscar alquiler por socio</label
        >
        <div class="relative rounded-xl shadow-sm group">
          <div class="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-4">
            <svg
              class="h-5 w-5 text-slate-400 group-focus-within:text-blue-500 transition-colors"
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 20 20"
              fill="currentColor"
              aria-hidden="true"
            >
              <path
                fill-rule="evenodd"
                d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
                clip-rule="evenodd"
              />
            </svg>
          </div>
          <input
            type="text"
            id="search-query"
            v-model="searchQuery"
            @keyup.enter="handleSearch"
            class="block w-full rounded-xl border-slate-300 pl-11 pr-10 focus:border-blue-500 focus:ring-2 focus:ring-blue-500 sm:text-sm px-3 py-3 border transition-all"
            placeholder="Ingrese DNI, nombre o apellido..."
          />
          <button
            v-if="searchQuery"
            @click="loadAlquileresActivos()"
            class="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-red-500 transition-colors"
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
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        </div>
        <!-- Socio selector when multiple matches -->
        <div v-if="showSocioSelector && sociosMatches.length > 0" class="mt-3">
          <p class="text-xs text-slate-500 mb-2 font-medium">Se encontraron {{ sociosMatches.length }} socios — seleccione uno:</p>
          <div class="space-y-1.5 max-h-60 overflow-y-auto pr-1">
            <button v-for="s in sociosMatches" :key="s.id" @click="selectSocioForAlquileres(s)"
              class="w-full text-left p-3 bg-white border border-slate-200 rounded-xl hover:bg-indigo-50 hover:border-indigo-200 transition-all flex justify-between items-center group">
              <div>
                <p class="text-sm font-bold text-slate-900 group-hover:text-indigo-700">{{ s.apellido }}, {{ s.nombre }}</p>
                <p class="text-xs text-slate-500">DNI: {{ s.dni }}</p>
              </div>
              <svg class="w-4 h-4 text-slate-300 group-hover:text-indigo-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
              </svg>
            </button>
          </div>
        </div>
      </div>
      <button
        @click="handleSearch"
        class="w-full sm:w-auto inline-flex justify-center items-center px-6 py-3 border border-transparent text-sm font-bold rounded-xl text-indigo-700 bg-indigo-100 hover:bg-indigo-200 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 transition-all shadow-sm transform active:scale-95"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          class="h-4 w-4 mr-2"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            stroke-width="2"
            d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
          />
        </svg>
        Buscar
      </button>
    </div>

    <!-- Loading -->
    <div v-if="loadingAlquileres" class="flex justify-center py-20">
      <svg
        class="animate-spin h-10 w-10 text-blue-600"
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

    <!-- Error -->
    <div
      v-if="searchError && !loadingAlquileres"
      class="rounded-xl bg-red-50 p-5 mb-8 border border-red-100 flex items-center gap-3 animate-in fade-in slide-in-from-top-2"
    >
      <svg
        class="h-5 w-5 text-red-400"
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 20 20"
        fill="currentColor"
      >
        <path
          fill-rule="evenodd"
          d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
          clip-rule="evenodd"
        />
      </svg>
      <span class="text-sm font-bold text-red-800">{{ searchError }}</span>
    </div>

    <!-- Results -->
    <div v-if="!loadingAlquileres">
      <h3 class="text-xs font-black text-slate-400 uppercase tracking-widest mb-6">
        {{ isSearching ? 'Resultados de búsqueda' : 'Alquileres Activos' }}
      </h3>

      <!-- List -->
      <div
        v-if="(isSearching ? searchResultAlquileres : alquileres).length > 0"
        class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8"
      >
        <AlquilerCard
          v-for="alq in isSearching ? searchResultAlquileres : alquileres"
          :key="alq.id"
          :alquiler="alq"
          @view-detail="goToAlquilerDetail"
        />
      </div>

      <!-- Pagination -->
      <div v-if="!isSearching && alquileres.length > 0" class="mt-10">
        <Pagination
          :current-page="currentPage"
          :total-pages="totalPages"
          :total-count="totalCount"
          :page-size="pageSize"
          @change-page="handlePageChange"
        />
      </div>

      <!-- Empty State -->
      <div
        v-else
        class="text-center py-20 bg-slate-50 rounded-3xl border-2 border-slate-200 border-dashed"
      >
        <div class="p-4 bg-white rounded-full w-fit mx-auto shadow-sm mb-4">
          <svg
            class="h-10 w-10 text-slate-300"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            aria-hidden="true"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
            />
          </svg>
        </div>
        <h3 class="mt-2 text-lg font-bold text-slate-900">No se encontraron alquileres</h3>
        <p class="mt-1 text-sm text-slate-500 max-w-sm mx-auto">
          {{
            isSearching
              ? 'Intente con otro DNI o borre el filtro para ver todos.'
              : 'No hay alquileres activos registrados en el sistema.'
          }}
        </p>
      </div>
    </div>
  </div>
</template>
