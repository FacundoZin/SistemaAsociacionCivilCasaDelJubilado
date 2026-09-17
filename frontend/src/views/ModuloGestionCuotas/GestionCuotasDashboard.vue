<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import SocioFeeCard from '../../components/ModuloGestionCuotas/SocioFeeCard.vue'
import CuotasService from '../../services/CuotasService'
import SociosService from '../../services/SociosService'

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

// State
const currentAction = ref('none') // 'none', 'pay', 'update'
const searchQuery = ref('')
const searchDni = searchQuery // keep alias for compat
const searchResult = ref(null)
const searchResults = ref([])
const searchError = ref('')
const isSearching = ref(false)
const isProcessing = ref(false)
const searchTotalCount = ref(0)

const formaPagoSelected = ref('2') // Default Sede (2)
const nuevoValorCuota = ref(0)
const selectedPeriods = ref([])

const handlePeriodsUpdate = (periods) => {
  selectedPeriods.value = periods
}

const paymentStepRef = ref(null)

const scrollToPayment = () => {
  setTimeout(() => {
    paymentStepRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, 100)
}

const actions = [
  {
    id: 'pay',
    title: 'Registrar pago de cuota',
    description: 'Buscar un socio por DNI y registrar el pago del semestre actual.',
    icon: 'M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z',
    color: 'text-emerald-600',
    bg: 'bg-emerald-50',
    hoverBorder: 'group-hover:border-emerald-200',
  },
  {
    id: 'update',
    title: 'Actualizar valor de cuota',
    description: 'Modificar el monto de la cuota social vigente para todos los socios.',
    icon: 'M12 4v16m8-8H4',
    color: 'text-blue-600',
    bg: 'bg-blue-50',
    hoverBorder: 'group-hover:border-blue-200',
  },
  {
    id: 'history',
    title: 'Ver historial de cuotas',
    description: 'Consultar el historial de pagos registrados filtrando por fecha o periodo.',
    icon: 'M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z',
    color: 'text-purple-600',
    bg: 'bg-purple-50',
    hoverBorder: 'group-hover:border-purple-200',
  },
]

const selectAction = (actionId) => {
  if (actionId === 'history') {
    router.push('/cuotas/historial')
    return
  }

  currentAction.value = actionId
  searchResult.value = null
  searchResults.value = []
  searchQuery.value = ''
  searchError.value = ''
  searchTotalCount.value = 0
  nuevoValorCuota.value = 0
  selectedPeriods.value = []
}

const goHome = () => {
  router.push('/')
}

const selectSocioFromResults = async (socio) => {
  // Fetch full debt preview for selected socio to keep existing SocioFeeCard expectations
  isSearching.value = true
  searchError.value = ''
  try {
    const full = await SociosService.getByDni(socio.dni)
    searchResult.value = full
    searchResults.value = []
    searchTotalCount.value = 0
  } catch (e) {
    // fallback to preview data if getByDni fails
    searchResult.value = socio
    searchResults.value = []
  } finally {
    isSearching.value = false
  }
}

const handleSearch = async () => {
  const queryClean = searchQuery.value.replace(/\s+/g, ' ').trim()
  if (!queryClean) return

  isSearching.value = true
  searchError.value = ''
  searchResult.value = null
  searchResults.value = []
  searchTotalCount.value = 0
  selectedPeriods.value = []

  try {
    const data = await SociosService.search(queryClean, 1, 20)

    let items = []
    if (Array.isArray(data)) items = data
    else if (data && Array.isArray(data.items)) items = data.items
    else if (data && data.id) items = [data]

    searchTotalCount.value = data?.totalCount ?? items.length

    if (items.length === 0) {
      searchError.value = 'No se encontraron socios'
    } else if (items.length === 1) {
      // Single result -> auto fetch full debt preview
      const socio = items[0]
      try {
        const full = await SociosService.getByDni(socio.dni)
        searchResult.value = full
      } catch {
        searchResult.value = socio
      }
    } else {
      searchResults.value = items
    }
  } catch (error) {
    searchError.value = error.message
  } finally {
    isSearching.value = false
  }
}

const clearSelection = () => {
  searchResult.value = null
  searchResults.value = []
  searchQuery.value = ''
  searchError.value = ''
  selectedPeriods.value = []
}

const handleRegisterPayment = async () => {
  if (!searchResult.value) return

  if (selectedPeriods.value.length === 0) {
    showToast('Debe seleccionar al menos un periodo para pagar', 'error')
    return
  }

  isProcessing.value = true
  try {
    const paymentData = {
      socioId: searchResult.value.id,
      formaPago: parseInt(formaPagoSelected.value),
      periodos: selectedPeriods.value,
    }

    if (formaPagoSelected.value === '0') {
      await CuotasService.registrarPagoCobrador(paymentData)
    } else {
      await CuotasService.registrarCuota(paymentData)
    }

    showToast('Los pagos se registraron correctamente')
    searchResult.value = null
    searchResults.value = []
    searchQuery.value = ''
    selectedPeriods.value = []
  } catch (error) {
    showToast(error.message || 'Algo salió mal al registrar el pago', 'error')
  } finally {
    isProcessing.value = false
  }
}

const handleUpdateValue = async () => {
  if (nuevoValorCuota.value <= 0) {
    showToast('El valor debe ser mayor a 0', 'error')
    return
  }

  isProcessing.value = true
  try {
    await CuotasService.actualizarValor(nuevoValorCuota.value)

    showToast('El valor de la cuota se actualizó correctamente')
    nuevoValorCuota.value = 0
  } catch (error) {
    showToast(error.message || 'Algo salió mal lo sentimos', 'error')
  } finally {
    isProcessing.value = false
  }
}

const handleView = (socio) => {
  router.push(`/socios/${socio.id}`)
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 font-sans text-slate-800">
    <!-- Header Institucional -->
    <!-- Header Institucional -->

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
                <span class="ml-1 text-sm font-medium text-slate-700 md:ml-2">Gestión de Cuotas</span>
              </div>
            </li>
          </ol>
        </nav>
        <h2 class="text-3xl font-bold text-slate-900 tracking-tight">Gestión de Cuotas</h2>
        <p class="text-slate-500 mt-1 text-lg">
          Administre el registro de pagos y el valor de la cuota social.
        </p>
      </div>

      <!-- Main Content Container with Transition -->
      <Transition mode="out-in" enter-active-class="transition duration-400 ease-out"
        enter-from-class="opacity-0 translate-y-4" enter-to-class="opacity-100 translate-y-0"
        leave-active-class="transition duration-300 ease-in" leave-from-class="opacity-100 translate-y-0"
        leave-to-class="opacity-0 -translate-y-4">

        <!-- FIRST VIEW: Initial Action Selector -->
        <div v-if="currentAction === 'none'" key="selector"
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 mb-10">
          <button v-for="action in actions" :key="action.id" @click="selectAction(action.id)"
            class="group relative flex flex-col p-6 bg-white rounded-3xl border border-slate-200 shadow-sm hover:shadow-xl hover:-translate-y-1 transition-all duration-300 text-left overflow-hidden"
            :class="[action.hoverBorder]">
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
                class="text-lg font-bold text-slate-900 mb-1 group-hover:text-emerald-700 transition-colors duration-300"
                :class="{ 'group-hover:text-blue-700': action.id === 'update', 'group-hover:text-purple-700': action.id === 'history' }">
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
          <!-- Pill Navbar -->
          <div class="flex flex-col sm:flex-row items-center gap-4 mb-8">
            <div
              class="flex flex-wrap items-center gap-2 p-1.5 bg-slate-200/50 rounded-[2rem] w-full sm:w-auto border border-slate-200/50 backdrop-blur-sm shadow-inner">
              <!-- Solo tabs para acciones inline (pay, update) -->
              <button v-for="action in actions.filter(a => a.id !== 'history')" :key="action.id"
                @click="selectAction(action.id)"
                class="flex-1 sm:flex-none flex items-center justify-center px-5 py-2.5 rounded-[1.5rem] transition-all duration-300 font-bold text-sm"
                :class="[
                  currentAction === action.id
                    ? 'bg-white text-emerald-600 shadow-md border border-slate-200 translate-y-[-1px] scale-[1.02]'
                    : 'text-slate-500 hover:text-slate-700 hover:bg-white/40'
                ]">
                <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" :d="action.icon" />
                </svg>
                {{ action.title }}
              </button>

              <!-- Tab separado para navegar al historial -->
              <button @click="selectAction('history')"
                class="flex-1 sm:flex-none flex items-center justify-center px-5 py-2.5 rounded-[1.5rem] transition-all duration-300 font-bold text-sm text-slate-500 hover:text-slate-700 hover:bg-white/40">
                <svg class="w-4 h-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>
                Historial
                <!-- Indicador de link externo -->
                <svg class="w-3 h-3 ml-1 opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14" />
                </svg>
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

          <!-- Dynamic Content Container -->
          <div class="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 min-h-[400px] overflow-hidden">
            <!-- PAY ACTION -->
            <div v-if="currentAction === 'pay'"
              class="max-w-2xl mx-auto animate-in fade-in slide-in-from-bottom-4 duration-500">
              <h3 class="text-xl font-bold text-slate-900 mb-6 flex items-center">
                <span
                  class="flex items-center justify-center w-8 h-8 rounded-full bg-emerald-100 text-emerald-600 text-sm mr-3">1</span>
                Registrar Pago de Cuota
              </h3>

              <div class="mb-8">
                <label for="search-query" class="block text-sm font-medium text-slate-700 mb-2 font-bold">Ingrese DNI, nombre o apellido</label>
                <div class="flex flex-col sm:flex-row gap-3">
                  <div class="relative flex-grow">
                    <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                      <svg class="h-5 w-5 text-slate-400" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20"
                        fill="currentColor">
                        <path fill-rule="evenodd"
                          d="M8 4a4 4 0 100 8 4 4 0 000-8zM2 8a6 6 0 1110.89 3.476l4.817 4.817a1 1 0 01-1.414 1.414l-4.816-4.816A6 6 0 012 8z"
                          clip-rule="evenodd" />
                      </svg>
                    </div>
                    <input type="text" id="search-query" v-model="searchQuery" @keyup.enter="handleSearch"
                      class="block w-full pl-10 pr-3 py-3 border border-slate-300 rounded-xl bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 sm:text-sm transition-all"
                      placeholder="Ej: 12345678, Juan Pérez" />
                  </div>
                  <button @click="handleSearch" :disabled="isSearching"
                    class="inline-flex items-center justify-center px-6 py-3 border border-transparent text-sm font-bold rounded-xl shadow-lg text-white bg-emerald-600 hover:bg-emerald-700 focus:outline-none disabled:opacity-50 transition-all">
                    <svg v-if="isSearching" class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" fill="none"
                      viewBox="0 0 24 24">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor"
                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z">
                      </path>
                    </svg>
                    {{ isSearching ? 'Buscando...' : 'Buscar Socio' }}
                  </button>
                </div>
              </div>

              <!-- Error Messages -->
              <div v-if="searchError"
                class="rounded-2xl bg-red-50 p-4 mb-6 border border-red-200 animate-in fade-in zoom-in duration-300">
                <div class="flex">
                  <div class="flex-shrink-0">
                    <svg class="h-5 w-5 text-red-400" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20"
                      fill="currentColor">
                      <path fill-rule="evenodd"
                        d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                        clip-rule="evenodd" />
                    </svg>
                  </div>
                  <div class="ml-3">
                    <p class="text-sm font-bold text-red-800">{{ searchError }}</p>
                  </div>
                </div>
              </div>

              <!-- Multiple results selector -->
              <div v-if="searchResults.length > 1" class="mb-6 animate-in fade-in slide-in-from-top-2 duration-300">
                <p class="text-sm text-slate-500 mb-3 font-medium">Se encontraron {{ searchTotalCount }} socios — seleccione uno:</p>
                <div class="space-y-2 max-h-80 overflow-y-auto pr-1">
                  <button v-for="s in searchResults" :key="s.id" @click="selectSocioFromResults(s)"
                    class="w-full text-left p-4 bg-white border border-slate-200 rounded-xl hover:bg-emerald-50 hover:border-emerald-200 transition-all flex justify-between items-center group">
                    <div>
                      <p class="text-sm font-bold text-slate-900 group-hover:text-emerald-700">{{ s.apellido }}, {{ s.nombre }}</p>
                      <p class="text-xs text-slate-500">DNI: {{ s.dni }}<span v-if="s.localidad"> — {{ s.localidad }}</span></p>
                    </div>
                    <svg class="w-5 h-5 text-slate-300 group-hover:text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
                    </svg>
                  </button>
                </div>
                <button @click="clearSelection" class="mt-3 text-xs font-bold text-slate-500 hover:text-slate-700">Limpiar búsqueda</button>
              </div>

              <!-- Search Results & Register Form -->
              <div v-if="searchResult" class="animate-in fade-in slide-in-from-top-4 duration-500">
                <div v-if="searchResults.length === 0" class="mb-4 flex justify-end">
                  <button @click="clearSelection" class="text-xs font-bold text-slate-500 hover:text-red-500 flex items-center gap-1">
                    <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" /></svg>
                    Cambiar socio
                  </button>
                </div>
                <SocioFeeCard :socio="searchResult" @view="handleView" @update-selection="handlePeriodsUpdate" />

                <!-- Guía visual para continuar al pago -->
                <div v-if="selectedPeriods.length > 0" class="mt-6 flex justify-center animate-bounce-slow">
                  <button @click="scrollToPayment"
                    class="inline-flex items-center gap-2 px-6 py-3 bg-emerald-100 text-emerald-700 font-bold rounded-2xl border border-emerald-200 shadow-sm hover:bg-emerald-200 transition-all group">
                    <span>Continuar al Registro de Pago</span>
                    <svg class="w-5 h-5 group-hover:translate-y-1 transition-transform" fill="none" viewBox="0 0 24 24"
                      stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                    </svg>
                  </button>
                </div>

                <div v-if="searchResult.adeudaCuotas || selectedPeriods.length > 0" ref="paymentStepRef"
                  class="mt-8 p-6 bg-slate-50 rounded-2xl border border-slate-200 shadow-sm">
                  <h4 class="text-lg font-bold text-slate-900 mb-4 flex items-center">
                    <svg class="w-5 h-5 mr-2 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
                    </svg>
                    Paso 2: Registrar Pago
                  </h4>

                  <div class="grid grid-cols-1 gap-4 mb-6">
                    <div>
                      <label class="block text-sm font-bold text-slate-700 mb-2">Forma de Pago</label>
                      <select v-model="formaPagoSelected"
                        class="block w-full py-3 px-4 border border-slate-300 bg-white rounded-xl shadow-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 sm:text-sm transition-all">
                        <option value="0">Cobrador</option>
                        <option value="1">Link de Pago</option>
                        <option value="2">Sede</option>
                      </select>
                    </div>
                  </div>

                  <button @click="handleRegisterPayment" :disabled="isProcessing"
                    class="w-full inline-flex justify-center items-center px-4 py-4 border border-transparent text-sm font-bold rounded-xl shadow-lg text-white bg-emerald-600 hover:bg-emerald-700 focus:outline-none disabled:opacity-50 transition-all">
                    <svg v-if="isProcessing" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white"
                      xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                      <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                      <path class="opacity-75" fill="currentColor"
                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z">
                      </path>
                    </svg>
                    {{ isProcessing ? 'Procesando...' : 'Confirmar Registro de Pago' }}
                  </button>
                </div>
              </div>
            </div>

            <!-- UPDATE ACTION -->
            <div v-else-if="currentAction === 'update'"
              class="max-w-md mx-auto py-12 animate-in fade-in zoom-in duration-300">
              <div class="bg-blue-50 rounded-2xl p-6 border border-blue-100 mb-8 shadow-sm">
                <div class="flex">
                  <div class="flex-shrink-0">
                    <div class="p-2 bg-blue-100 rounded-xl">
                      <svg class="h-6 w-6 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                          d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                    </div>
                  </div>
                  <div class="ml-4">
                    <h3 class="text-base font-bold text-blue-900 tracking-tight">Actualizar Valor de Cuota</h3>
                    <p class="text-sm text-blue-700 mt-1 leading-relaxed">
                      El nuevo valor se aplicará a todos los registros de pago futuros para todos los socios.
                    </p>
                  </div>
                </div>
              </div>

              <div class="space-y-6">
                <div>
                  <label for="nuevo-valor" class="block text-sm font-bold text-slate-700 mb-2 px-1">Nuevo Valor Mensual
                    ($)</label>
                  <div class="relative">
                    <div class="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                      <span class="text-slate-500 font-bold">$</span>
                    </div>
                    <input type="number" id="nuevo-valor" v-model="nuevoValorCuota" step="0.01"
                      class="block w-full pl-9 pr-4 py-3 border border-slate-300 rounded-xl bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 sm:text-sm shadow-sm transition-all"
                      placeholder="0.00" />
                  </div>
                </div>

                <button @click="handleUpdateValue" :disabled="isProcessing"
                  class="w-full inline-flex justify-center items-center px-6 py-3.5 border border-transparent text-sm font-bold rounded-xl shadow-lg text-white bg-blue-600 hover:bg-blue-700 focus:outline-none disabled:opacity-50 transition-all hover:-translate-y-0.5">
                  <svg v-if="isProcessing" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white"
                    xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z">
                    </path>
                  </svg>
                  {{ isProcessing ? 'Actualizando...' : 'Guardar Nuevo Valor' }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </Transition>

      <!-- Dynamic Content Area end -->
    </main>

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

<style scoped>
@keyframes bounce-slow {

  0%,
  100% {
    transform: translateY(-5%);
    animation-timing-function: cubic-bezier(0.8, 0, 1, 1);
  }

  50% {
    transform: translateY(0);
    animation-timing-function: cubic-bezier(0, 0, 0.2, 1);
  }
}

.animate-bounce-slow {
  animation: bounce-slow 2s infinite;
}
</style>
