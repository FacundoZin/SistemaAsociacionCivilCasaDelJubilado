<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import ConfirmModal from '../../components/Common/ConfirmModal.vue'
import Pagination from '../../components/Common/Pagination.vue'
import SocioCard from '../../components/ModuloGestionSocios/SocioCard.vue'
import SocioFormModal from '../../components/ModuloGestionSocios/SocioFormModal.vue'
import SocioList from '../../components/ModuloGestionSocios/SocioList.vue'
import SocioUpdateModal from '../../components/ModuloGestionSocios/SocioUpdateModal.vue'
import SociosService from '../../services/SociosService'

// State
const currentAction = ref('none') // 'none', 'add', 'search', 'debtors'
const router = useRouter()

// Toast State
const toast = ref({
  show: false,
  message: '',
  type: 'success',
})

const showToast = (message, type = 'success') => {
  toast.value = { show: true, message, type }
  const duration = type === 'error' ? 6000 : 3000
  setTimeout(() => {
    toast.value.show = false
  }, duration)
}

// Modal State
const isModalOpen = ref(false)
const isUpdateModalOpen = ref(false)
const isConfirmDeleteOpen = ref(false)
const socioToDelete = ref(null)
const selectedSocio = ref(null) // For reference
const selectedSocioId = ref(null)

// Search State
const searchQuery = ref('')
const searchDni = searchQuery // compat alias for template if needed
const searchResult = ref(null)
const searchResults = ref([])
const searchError = ref('')
const isSearching = ref(false)
const searchPage = ref(1)
const searchPageSize = ref(10)
const searchTotalCount = ref(0)
const searchTotalPages = ref(0)

// Debtors State
const debtorsList = ref([])
const isLoadingDebtors = ref(false)
const debtorsError = ref('')
const currentPage = ref(1)
const pageSize = ref(9)
const totalPages = ref(0)
const totalCount = ref(0)

// Actions Configuration
const actions = [
  {
    id: 'add',
    title: 'Agregar socio',
    description: 'Registrar un nuevo socio en el padrón.',
    icon: 'M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z',
    color: 'text-blue-600',
    bg: 'bg-blue-50',
    hoverBorder: 'group-hover:border-blue-200',
  },
  {
    id: 'search',
    title: 'Buscar socio',
    description: 'Consultar estado e información de un socio.',
    icon: 'M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z',
    color: 'text-indigo-600',
    bg: 'bg-indigo-50',
    hoverBorder: 'group-hover:border-indigo-200',
  },
  {
    id: 'debtors',
    title: 'Ver socios deudores',
    description: 'Listado de socios con cuotas pendientes.',
    icon: 'M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z',
    color: 'text-rose-600',
    bg: 'bg-rose-50',
    hoverBorder: 'group-hover:border-rose-200',
  },
]

const selectAction = (actionId) => {
  currentAction.value = actionId
  // Reset states
  searchResult.value = null
  searchResults.value = []
  searchQuery.value = ''
  searchError.value = ''
  searchTotalCount.value = 0
  searchTotalPages.value = 0
  searchPage.value = 1
  debtorsList.value = []
  debtorsError.value = ''

  if (actionId === 'add') {
    openModal()
  } else if (actionId === 'debtors') {
    fetchDebtors()
  }
}

const goHome = () => {
  router.push('/')
}

// Modal Logic
const openModal = (socio = null) => {
  selectedSocio.value = socio
  isModalOpen.value = true
}

const closeModal = () => {
  isModalOpen.value = false
  isUpdateModalOpen.value = false
  isConfirmDeleteOpen.value = false
  selectedSocio.value = null
  selectedSocioId.value = null
  socioToDelete.value = null
}

const handleSaveSocio = (savedSocio) => {
  closeModal()
  // Refresh data if needed
  const activeId = savedSocio?.id ?? selectedSocioId.value
  const hasActiveSearch = currentAction.value === 'search' && (searchResult.value || searchResults.value.length > 0)
  const matchesSearch = hasActiveSearch && (searchResults.value.some(s => s.id === activeId) || searchResult.value?.id === activeId)
  if (matchesSearch) {
    if (savedSocio?.dni) {
      searchQuery.value = savedSocio.dni
    }
    handleSearch(1)
  } else if (currentAction.value === 'debtors') {
    fetchDebtors()
  }

  showToast('Operación realizada con éxito')
}

// Search Logic
const handleSearch = async (page = 1) => {
  const queryClean = searchQuery.value.replace(/\s+/g, ' ').trim()
  if (!queryClean) return

  isSearching.value = true
  searchError.value = ''
  searchResult.value = null
  searchResults.value = []
  searchPage.value = page

  try {
    const data = await SociosService.search(queryClean, page, searchPageSize.value)

    // Normalize PagedResult shape: { items, totalCount, totalPages } or plain array
    let items = []
    let totalCount = 0
    let totalPages = 0
    if (Array.isArray(data)) {
      items = data
      totalCount = data.length
      totalPages = 1
    } else if (data && Array.isArray(data.items)) {
      items = data.items
      totalCount = data.totalCount ?? items.length
      totalPages = data.totalPages ?? Math.ceil(totalCount / searchPageSize.value)
    } else if (data && data.id) {
      items = [data]
      totalCount = 1
      totalPages = 1
    } else {
      items = []
    }

    searchResults.value = items
    searchTotalCount.value = totalCount
    searchTotalPages.value = totalPages
    if (items.length === 0) {
      searchError.value = 'No se encontraron socios'
    } else if (items.length === 1) {
      searchResult.value = items[0]
    }
  } catch (error) {
    searchError.value = error.message
    searchResults.value = []
    searchTotalCount.value = 0
    searchTotalPages.value = 0
  } finally {
    isSearching.value = false
  }
}

const handleSearchPageChange = (newPage) => {
  handleSearch(newPage)
}

// Debtors Logic
const fetchDebtors = async (page = 1) => {
  currentPage.value = page
  isLoadingDebtors.value = true
  debtorsError.value = ''

  try {
    const data = await SociosService.getDebtors(page, pageSize.value)
    debtorsList.value = data.items
    totalCount.value = data.totalCount
    totalPages.value = data.totalPages
  } catch (error) {
    debtorsError.value = error.message
  } finally {
    isLoadingDebtors.value = false
  }
}

// Card Actions
const handleEdit = (socio) => {
  selectedSocioId.value = socio.id
  isUpdateModalOpen.value = true
}

const handleDelete = (socio) => {
  socioToDelete.value = socio
  isConfirmDeleteOpen.value = true
}

const confirmDelete = async () => {
  if (!socioToDelete.value) return

  const socio = socioToDelete.value

  try {
    await SociosService.removeSocio(socio.id)

    // Refresh
    if (currentAction.value === 'search') {
      searchResult.value = null
      searchResults.value = []
      searchQuery.value = ''
      searchTotalCount.value = 0
      searchTotalPages.value = 0
    } else if (currentAction.value === 'debtors') {
      fetchDebtors()
    }

    showToast('Socio eliminado correctamente')
    closeModal()
  } catch (error) {
    closeModal()
    showToast(error.message, 'error')
  }
}

const handleView = (socio) => {
  router.push(`/socios/${socio.id}`)
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 font-sans text-slate-800">
    <!-- Header Institucional (Consistent with Home) -->
    <!-- Header Institucional (Consistent with Home) -->

    <!-- Main Content -->
    <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <!-- Breadcrumb & Page Title -->
      <div class="mb-8">
        <nav class="flex mb-2" aria-label="Breadcrumb">
          <ol class="inline-flex items-center space-x-1 md:space-x-3">
            <li class="inline-flex items-center">
              <a href="#" @click.prevent="goHome"
                class="inline-flex items-center text-sm font-medium text-slate-500 hover:text-blue-600">
                <svg class="w-3 h-3 mr-2.5" aria-hidden="true" xmlns="http://www.w3.org/2000/svg" fill="currentColor"
                  viewBox="0 0 20 20">
                  <path
                    d="m19.707 9.293-2-2-7-7a1 1 0 0 0-1.414 0l-7 7-2 2a1 1 0 0 0 1.414 1.414L2 10.414V18a2 2 0 0 0 2 2h3a1 1 0 0 0 1-1v-4a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v4a1 1 0 0 0 1 1h3a2 2 0 0 0 2-2v-7.586l.293.293a1 1 0 0 0 1.414-1.414Z" />
                </svg>
                Inicio
              </a>
            </li>
            <li>
              <div class="flex items-center">
                <svg class="w-3 h-3 text-slate-400 mx-1" aria-hidden="true" xmlns="http://www.w3.org/2000/svg"
                  fill="none" viewBox="0 0 6 10">
                  <path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="m1 9 4-4-4-4" />
                </svg>
                <span class="ml-1 text-sm font-medium text-slate-700 md:ml-2">Gestión de Socios</span>
              </div>
            </li>
          </ol>
        </nav>
        <h2 class="text-3xl font-bold text-slate-900 tracking-tight">Gestión de Socios</h2>
        <p class="text-slate-500 mt-1 text-lg">Seleccione una acción para administrar socios.</p>
      </div>

      <!-- Coordinated Transition for Main Views -->
      <Transition mode="out-in" enter-active-class="transition duration-400 ease-out"
        enter-from-class="opacity-0 translate-y-4" enter-to-class="opacity-100 translate-y-0"
        leave-active-class="transition duration-300 ease-in" leave-from-class="opacity-100 translate-y-0"
        leave-to-class="opacity-0 -translate-y-4">
        <!-- FIRST VIEW: Initial Action Selector -->
        <div v-if="currentAction === 'none'" key="selector"
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 mb-10">
          <button v-for="action in actions" :key="action.id" @click="selectAction(action.id)"
            class="group relative flex flex-col p-6 bg-white rounded-3xl border border-slate-200 shadow-sm hover:shadow-xl hover:-translate-y-1 transition-all duration-300 text-left overflow-hidden">
            <!-- Hover Background Effect -->
            <div
              class="absolute inset-0 bg-gradient-to-br from-slate-50 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300">
            </div>

            <div class="relative z-10 flex items-start justify-between mb-5">
              <div
                class="p-4 rounded-2xl transition-all duration-300 shadow-sm ring-1 ring-black/5 group-hover:scale-110"
                :class="[action.bg, action.color]">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8" fill="none" viewBox="0 0 24 24"
                  stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" :d="action.icon" />
                </svg>
              </div>
            </div>

            <div class="relative z-10 mt-auto">
              <h3
                class="text-xl font-bold text-slate-900 mb-1 group-hover:text-blue-700 transition-colors duration-300">
                {{ action.title }}
              </h3>
              <p class="text-sm text-slate-500 leading-relaxed font-medium">
                {{ action.description }}
              </p>
            </div>
          </button>
        </div>

        <!-- SECOND VIEW: Active Module (Navbar + Content Container) -->
        <div v-else key="content-view">
          <!-- Navigation Pill-Navbar -->
          <div class="flex flex-col sm:flex-row items-center gap-4 mb-8">
            <div
              class="flex flex-wrap items-center gap-2 p-1.5 bg-slate-200/50 rounded-[2rem] w-full sm:w-auto border border-slate-200/50 backdrop-blur-sm shadow-inner">
              <button v-for="action in actions" :key="action.id" @click="selectAction(action.id)"
                class="flex-1 sm:flex-none flex items-center justify-center px-5 py-2.5 rounded-[1.5rem] transition-all duration-300 font-bold text-sm"
                :class="[
                  currentAction === action.id
                    ? 'bg-white text-blue-600 shadow-md border border-slate-200 translate-y-[-1px] scale-[1.02]'
                    : 'text-slate-500 hover:text-slate-700 hover:bg-white/40',
                ]">
                <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="action.icon" />
                </svg>
                {{ action.title }}
              </button>
            </div>

            <button @click="currentAction = 'none'"
              class="hidden sm:flex items-center px-4 py-2 text-slate-400 hover:text-red-500 font-bold text-sm transition-colors group">
              <svg class="w-4 h-4 mr-2 group-hover:-translate-x-1 transition-transform" fill="none" viewBox="0 0 24 24"
                stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
              </svg>
              Volver
            </button>
          </div>

          <!-- Dynamic Content Area -->
          <div class="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 min-h-[400px] overflow-hidden">
            <!-- ADD ACTION -->
            <div v-if="currentAction === 'add'"
              class="flex flex-col items-center justify-center h-full py-12 text-center animate-in fade-in zoom-in duration-300">
              <div class="p-4 bg-blue-50 rounded-full mb-4">
                <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-blue-600" fill="none" viewBox="0 0 24 24"
                  stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" />
                </svg>
              </div>
              <h3 class="text-lg font-bold text-slate-900">Registrar nuevo socio</h3>
              <p class="text-slate-500 mt-2 max-w-md">
                El formulario se ha abierto en una ventana emergente. Si la cerró, puede volver a
                abrirla con el botón de abajo.
              </p>
              <button @click="openModal()"
                class="mt-6 inline-flex items-center px-6 py-3 border border-transparent text-sm font-bold rounded-xl shadow-lg text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 transition-all">
                Abrir formulario
              </button>
            </div>

            <!-- SEARCH ACTION -->
            <div v-else-if="currentAction === 'search'"
              class="max-w-4xl mx-auto py-4 animate-in fade-in slide-in-from-bottom-4 duration-500">
              <div class="mb-8">
                <label for="search-query" class="block text-sm font-bold text-slate-700 mb-2">Ingrese DNI, nombre o apellido</label>
                <div class="flex gap-3">
                  <div class="relative flex-grow">
                    <div class="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                      <svg class="h-5 w-5 text-slate-400" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20"
                        fill="currentColor" aria-hidden="true">
                        <path fill-rule="evenodd"
                          d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
                          clip-rule="evenodd" />
                      </svg>
                    </div>
                    <input type="text" id="search-query" v-model="searchQuery" @keyup.enter="handleSearch(1)"
                      class="block w-full pl-11 pr-4 py-3 border border-slate-300 rounded-xl leading-5 bg-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 sm:text-sm shadow-sm transition-all"
                      placeholder="Ej: 12345678, Juan Pérez" />
                  </div>
                  <button @click="handleSearch(1)" :disabled="isSearching"
                    class="inline-flex items-center px-6 py-3 border border-transparent text-sm font-bold rounded-xl shadow-lg text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 transition-all">
                    {{ isSearching ? 'Buscando...' : 'Buscar' }}
                  </button>
                </div>
              </div>

              <!-- Search Error / Empty -->
              <div v-if="searchError && searchResults.length === 0"
                class="rounded-xl bg-red-50 p-4 mb-6 border border-red-100 animate-in fade-in slide-in-from-top-2">
                <div class="flex">
                  <div class="flex-shrink-0">
                    <svg class="h-5 w-5 text-red-400" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20"
                      fill="currentColor" aria-hidden="true">
                      <path fill-rule="evenodd"
                        d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                        clip-rule="evenodd" />
                    </svg>
                  </div>
                  <div class="ml-3">
                    <h3 class="text-sm font-bold text-red-800">{{ searchError }}</h3>
                  </div>
                </div>
              </div>

              <!-- Single result classic card -->
              <div v-if="searchResults.length === 1">
                <SocioCard :socio="searchResults[0]" @edit="handleEdit" @delete="handleDelete" @view="handleView" />
              </div>

              <!-- Multiple results -->
              <div v-else-if="searchResults.length > 1">
                <p class="text-sm text-slate-500 mb-3">Se encontraron {{ searchTotalCount }} socios</p>
                <SocioList :socios="searchResults" @edit="handleEdit" @delete="handleDelete" @view="handleView" />
                <div v-if="searchTotalPages > 1" class="mt-6">
                  <Pagination :current-page="searchPage" :total-pages="searchTotalPages" :total-count="searchTotalCount" :page-size="searchPageSize" @change-page="handleSearchPageChange" />
                </div>
              </div>
            </div>

            <!-- DEBTORS ACTION -->
            <div v-else-if="currentAction === 'debtors'" class="animate-in fade-in slide-in-from-bottom-4 duration-500">
              <div class="flex justify-between items-center mb-6">
                <h3 class="text-xl font-bold text-slate-900">Socios con deuda pendiente</h3>
                <button @click="fetchDebtors(currentPage)"
                  class="text-sm text-blue-600 hover:text-blue-800 font-bold flex items-center">
                  <svg class="w-4 h-4 mr-1" :class="{ 'animate-spin': isLoadingDebtors }" fill="none"
                    viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                  Actualizar lista
                </button>
              </div>

              <div v-if="isLoadingDebtors" class="flex justify-center py-12">
                <svg class="animate-spin h-8 w-8 text-blue-600" xmlns="http://www.w3.org/2000/svg" fill="none"
                  viewBox="0 0 24 24">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                  <path class="opacity-75" fill="currentColor"
                    d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z">
                  </path>
                </svg>
              </div>

              <div v-else-if="debtorsError" class="text-center py-12 text-red-600 font-bold">
                {{ debtorsError }}
              </div>

              <div v-else-if="totalCount > 0">
                <SocioList :socios="debtorsList" @edit="handleEdit" @delete="handleDelete" @view="handleView" />

                <!-- Pagination -->
                <div class="mt-8">
                  <Pagination :current-page="currentPage" :total-pages="totalPages" :total-count="totalCount"
                    :page-size="pageSize" @change-page="fetchDebtors" />
                </div>
              </div>

              <div v-else class="text-center py-20 text-slate-500">
                <svg class="w-16 h-16 mx-auto mb-4 text-slate-200" fill="none" viewBox="0 0 24 24"
                  stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <p class="text-lg font-medium">No hay socios con deudas pendientes.</p>
              </div>
            </div>
          </div>
        </div>
      </Transition>
      <!-- Dynamic Content Area end -->
    </main>

    <!-- Modal Component -->
    <SocioFormModal :is-open="isModalOpen" @close="closeModal" @save="handleSaveSocio" />

    <SocioUpdateModal :is-open="isUpdateModalOpen" :socio-id="selectedSocioId" @close="closeModal"
      @save="handleSaveSocio" />

    <!-- Confirmation Modal -->
    <ConfirmModal :is-open="isConfirmDeleteOpen" title="Confirmar Baja de Socio" :message="socioToDelete
        ? `¿Está seguro que desea dar de baja a ${socioToDelete.nombre} ${socioToDelete.apellido}? Esta acción no se puede deshacer.`
        : ''
      " confirm-text="Dar de Baja" type="danger" @close="closeModal" @confirm="confirmDelete" />

    <!-- Toast Notification -->
    <Transition enter-active-class="transform ease-out duration-300 transition"
      enter-from-class="translate-y-2 opacity-0 sm:translate-y-0 sm:translate-x-2"
      enter-to-class="translate-y-0 opacity-100 sm:translate-x-0" leave-active-class="transition ease-in duration-100"
      leave-from-class="opacity-100" leave-to-class="opacity-0">
      <div v-if="toast.show"
        class="fixed bottom-5 right-5 z-50 flex w-full max-w-sm overflow-hidden bg-white rounded-lg shadow-2xl border border-slate-200 pointer-events-auto ring-1 ring-black ring-opacity-5">
        <div class="flex items-center justify-center w-12" :class="{
          'bg-emerald-500': toast.type === 'success',
          'bg-blue-500': toast.type === 'info',
          'bg-red-500': toast.type === 'error',
        }">
          <svg v-if="toast.type === 'success'" class="w-6 h-6 text-white fill-current" viewBox="0 0 40 40"
            xmlns="http://www.w3.org/2000/svg">
            <path
              d="M20 3.33331C10.8 3.33331 3.33337 10.8 3.33337 20C3.33337 29.2 10.8 36.6666 20 36.6666C29.2 36.6666 36.6667 29.2 36.6667 20C36.6667 10.8 29.2 3.33331 20 3.33331ZM16.6667 28.3333L8.33337 20L10.6834 17.65L16.6667 23.6166L29.3167 10.9666L31.6667 13.3333L16.6667 28.3333Z" />
          </svg>
          <svg v-else-if="toast.type === 'info'" class="w-6 h-6 text-white fill-current" viewBox="0 0 24 24" fill="none"
            stroke="currentColor" xmlns="http://www.w3.org/2000/svg">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
              d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <svg v-else class="w-6 h-6 text-white fill-current" viewBox="0 0 40 40" xmlns="http://www.w3.org/2000/svg">
            <path
              d="M20 3.33331C10.8 3.33331 3.33337 10.8 3.33337 20C3.33337 29.2 10.8 36.6666 20 36.6666C29.2 36.6666 36.6667 29.2 36.6667 20C36.6667 10.8 29.2 3.33331 20 3.33331ZM21.6667 28.3333H18.3334V25H21.6667V28.3333ZM21.6667 21.6666H18.3334V11.6666H21.6667V21.6666Z" />
          </svg>
        </div>

        <div class="px-4 py-3 -mx-3">
          <div class="mx-3">
            <span class="font-semibold" :class="{
              'text-emerald-500': toast.type === 'success',
              'text-blue-500': toast.type === 'info',
              'text-red-500': toast.type === 'error',
            }">
              {{ toast.type === 'success' ? 'Éxito' : toast.type === 'info' ? 'Info' : 'Error' }}
            </span>
            <p class="text-sm text-slate-600">
              {{ toast.message }}
            </p>
          </div>
        </div>

        <button @click="toast.show = false" class="ml-auto p-2 text-slate-400 hover:text-slate-600 focus:outline-none">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </Transition>
  </div>
</template>
